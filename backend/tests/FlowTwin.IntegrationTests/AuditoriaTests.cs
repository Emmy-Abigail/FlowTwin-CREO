using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FlowTwin.Auditoria.Application;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.IntegrationTests;

/// <summary>P11 · Auditoría: consulta, CSV, intentos de ingreso fallidos y la cadena de sellos de integridad.</summary>
[Collection("Api")]
public class AuditoriaTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static string Hoy => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)).ToString("yyyy-MM-dd");
    private static string Periodo => $"desde={DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)).AddDays(-1):yyyy-MM-dd}&hasta={Hoy}";

    [Fact]
    public async Task Solo_el_Administrador_consulta_la_bitacora()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");
        var gerencia = await api.ClienteDeAsync("gerencia@creo.pe");

        Assert.Equal(HttpStatusCode.Forbidden, (await operador.GetAsync($"/api/v1/auditoria?{Periodo}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await gerencia.GetAsync($"/api/v1/auditoria?{Periodo}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Fabrica.CreateClient().GetAsync($"/api/v1/auditoria?{Periodo}")).StatusCode);
    }

    [Fact]
    public async Task Los_ingresos_fallidos_y_el_bloqueo_quedan_en_Accesos()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var creado = await Json(await admin.PostAsJsonAsync("/api/v1/usuarios",
            new { nombres = "Intentos", apellidos = "Fallidos", correo = "intentos.fallidos@creo.pe", rol = "OPERADOR", pisos = new[] { 1 } }));
        var id = creado.GetProperty("usuario").GetProperty("id").GetInt32().ToString();
        var anonimo = api.Fabrica.CreateClient();

        for (var i = 0; i < 5; i++)
            await anonimo.PostAsJsonAsync("/api/v1/auth/login", new { correo = "intentos.fallidos@creo.pe", clave = "equivocada-" + i });
        var noExiste = await anonimo.PostAsJsonAsync("/api/v1/auth/login", new { correo = "nadie.existe@creo.pe", clave = "lo-que-sea" });

        Assert.Equal(HttpStatusCode.Unauthorized, noExiste.StatusCode);   // a quien intenta, el mismo mensaje genérico de siempre
        var filas = await api.EnBaseAsync(db => db.Auditoria.Where(a => a.Entidad == "Usuario"
            && (a.AccionAuditoriaId == AccionAuditoria.InicioSesionFallido || a.AccionAuditoriaId == AccionAuditoria.CuentaBloqueada)).ToListAsync());
        Assert.Equal(5, filas.Count(a => a.EntidadId == id && a.AccionAuditoriaId == AccionAuditoria.InicioSesionFallido));
        Assert.Single(filas, a => a.EntidadId == id && a.AccionAuditoriaId == AccionAuditoria.CuentaBloqueada);
        // Quien falla no es el dueño de la cuenta: se registra a nombre de Sistema, con la cuenta afectada en la entidad
        Assert.All(filas.Where(a => a.EntidadId == id), a => Assert.Equal(UsuarioSistema.Id, a.UsuarioRegistroId));
        var desconocido = Assert.Single(filas, a => a.EntidadId is null && a.ValorDespues!.Contains("nadie.existe@creo.pe"));
        Assert.Contains("DESCONOCIDO", desconocido.ValorDespues);
        Assert.Equal(UsuarioSistema.Id, desconocido.UsuarioRegistroId);

        var pagina = await Json(await admin.GetAsync($"/api/v1/auditoria?{Periodo}&categoria=ACCESOS&buscar=intentos.fallidos"));
        Assert.True(pagina.GetProperty("total").GetInt32() >= 6);
        Assert.All(pagina.GetProperty("filas").EnumerateArray(), f => Assert.Equal("ACCESOS", f.GetProperty("categoria").GetString()));
    }

    [Fact]
    public async Task La_lista_trae_quien_que_y_sobre_que_con_los_conteos_y_el_detalle_el_antes_y_despues()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var creado = await Json(await admin.PostAsJsonAsync("/api/v1/usuarios",
            new { nombres = "Bitácora", apellidos = "Prueba", correo = "bitacora.prueba@creo.pe", rol = "OPERADOR", pisos = new[] { 1 } }));
        var id = creado.GetProperty("usuario").GetProperty("id").GetInt32();
        await admin.PutAsJsonAsync($"/api/v1/usuarios/{id}",
            new { nombres = "Bitácora", apellidos = "Editada", correo = "bitacora.prueba@creo.pe", rol = "OPERADOR", pisos = new[] { 1 } });

        var pagina = await Json(await admin.GetAsync($"/api/v1/auditoria?{Periodo}&categoria=USUARIOS&buscar=bitacora.prueba"));
        var edicion = pagina.GetProperty("filas").EnumerateArray().First(f => f.GetProperty("accion").GetString() == "EDITAR");
        var detalle = await Json(await admin.GetAsync($"/api/v1/auditoria/{edicion.GetProperty("id").GetInt64()}"));

        Assert.Equal("Bitácora Editada", edicion.GetProperty("objeto").GetString());
        var ingreso = (await Json(await admin.GetAsync($"/api/v1/auditoria?{Periodo}&categoria=ACCESOS"))).GetProperty("filas").EnumerateArray()
            .First(f => f.GetProperty("accion").GetString() == "INICIAR_SESION");
        Assert.Equal(ingreso.GetProperty("usuario").GetString(), ingreso.GetProperty("objeto").GetString());   // el nombre, no «2»
        Assert.False(string.IsNullOrEmpty(edicion.GetProperty("usuario").GetString()));   // el nombre del Administrador
        Assert.Equal("Editó", edicion.GetProperty("accionTexto").GetString());
        var conteos = pagina.GetProperty("conteos");
        foreach (var c in new[] { "ACCESOS", "USUARIOS", "CONFIGURACION", "DISPOSITIVOS", "OPERACION" }) Assert.True(conteos.TryGetProperty(c, out _));
        Assert.Equal("Prueba", detalle.GetProperty("antes").GetProperty("apellidos").GetString());
        // El código técnico de una fila la encuentra (exacto): es lo que un técnico copia de los registros del servidor
        var codigo = detalle.GetProperty("traceId").GetString()!;
        var porCodigo = await Json(await admin.GetAsync($"/api/v1/auditoria?{Periodo}&buscar={codigo}"));
        Assert.Equal(edicion.GetProperty("id").GetInt64(), Assert.Single(porCodigo.GetProperty("filas").EnumerateArray()).GetProperty("id").GetInt64());
        Assert.Equal("Editada", detalle.GetProperty("despues").GetProperty("apellidos").GetString());
    }

    [Fact]
    public async Task Filtros_invalidos_dan_un_error_claro()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");

        var categoria = await admin.GetAsync($"/api/v1/auditoria?{Periodo}&categoria=TODO");
        var periodo = await admin.GetAsync("/api/v1/auditoria?desde=2026-11-10&hasta=2026-11-01");

        Assert.Equal("CATEGORIA_INVALIDA", (await Json(categoria)).GetProperty("codigo").GetString());
        Assert.Equal("PERIODO_INVALIDO", (await Json(periodo)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task El_CSV_respeta_el_filtro_y_queda_registrado()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");

        var r = await admin.GetAsync($"/api/v1/auditoria/exportar.csv?{Periodo}&categoria=USUARIOS");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("text/csv", r.Content.Headers.ContentType?.MediaType);
        var texto = Encoding.UTF8.GetString(await r.Content.ReadAsByteArrayAsync());
        Assert.StartsWith("﻿Fecha y hora,Usuario,Acción,Categoría,Entidad,Objeto,IP", texto);
        Assert.True(await api.EnBaseAsync(db => db.Auditoria.AnyAsync(a => a.AccionAuditoriaId == AccionAuditoria.Exportar && a.EntidadId == "auditoria")));
    }

    [Fact]
    public async Task La_cadena_de_sellos_detecta_una_fila_editada_por_fuera_de_la_aplicacion()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        await admin.PostAsJsonAsync("/api/v1/usuarios",
            new { nombres = "Sello", apellidos = "Integridad", correo = "sello.integridad@creo.pe", rol = "OPERADOR", pisos = new[] { 1 } });
        using (var scope = api.Fabrica.Services.CreateScope())
        {
            var sello = scope.ServiceProvider.GetRequiredService<SelladoAuditoriaServicio>();
            while (await sello.SellarPendientesAsync(CancellationToken.None) > 0) { }
        }

        var integra = await Json(await admin.PostAsync($"/api/v1/auditoria/integridad?{Periodo}", null));
        Assert.True(integra.GetProperty("configurada").GetBoolean());
        Assert.True(integra.GetProperty("integra").GetBoolean());
        Assert.True(integra.GetProperty("revisados").GetInt32() > 0);

        // Alguien con acceso a SQL Server cambia una fila ya sellada
        var (filaId, original) = await api.EnBaseAsync(async db =>
        {
            var f = await db.Auditoria.Where(a => a.ValorDespues!.Contains("sello.integridad@creo.pe")).OrderBy(a => a.Id).FirstAsync();
            return (f.Id, f.ValorDespues);
        });
        await api.EnBaseAsync(db => db.Auditoria.Where(a => a.Id == filaId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ValorDespues, "{\"correo\":\"otro@creo.pe\"}")));
        try
        {
            var alterada = await Json(await admin.PostAsync($"/api/v1/auditoria/integridad?{Periodo}", null));

            Assert.False(alterada.GetProperty("integra").GetBoolean());
            Assert.Equal(filaId, alterada.GetProperty("primeraAlteracionId").GetInt64());
        }
        finally
        {
            // Se deja la base como estaba para las demás pruebas
            await api.EnBaseAsync(db => db.Auditoria.Where(a => a.Id == filaId).ExecuteUpdateAsync(s => s.SetProperty(a => a.ValorDespues, original)));
        }
    }
}
