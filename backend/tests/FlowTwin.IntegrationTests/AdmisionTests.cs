using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.IntegrationTests;

/// <summary>Flujos F-06, F-08 y F-09 y los permisos, de punta a punta: HTTP → reglas → SQL Server.</summary>
[Collection("Api")]
public class AdmisionTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static Task<HttpResponseMessage> Escanear(HttpClient c, string codigo, string modo = "AUTO") =>
        c.PostAsJsonAsync("/api/v1/admision/escaneos", new { codigoQr = codigo, modo });

    /// <summary>
    /// Deja la tarjeta «por desinfectar» como en la vida real: se entrega y se devuelve. Si otra prueba
    /// ya la dejó así, no hace nada (las pruebas comparten la base y corren en cualquier orden).
    /// </summary>
    private static async Task DejarPorDesinfectarAsync(HttpClient operador, string codigo)
    {
        var previa = await Json(await operador.GetAsync($"/api/v1/admision/vista-previa?qr={codigo}"));
        if (previa.GetProperty("estado").GetString() == "POR_DESINFECTAR") return;
        if (previa.GetProperty("estado").GetString() == "DISPONIBLE") (await Escanear(operador, codigo)).EnsureSuccessStatusCode();
        (await Escanear(operador, codigo)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Entrega_devolucion_y_desinfeccion_completan_el_ciclo_de_la_tarjeta()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");

        var entrega = await Json(await Escanear(operador, TarjetasDePrueba.CicloCompleto));
        Assert.Equal("ENTREGA", entrega.GetProperty("operacion").GetString());
        Assert.Equal("EN_USO", entrega.GetProperty("tarjeta").GetProperty("estado").GetString());
        var codigo = entrega.GetProperty("atencion").GetProperty("codigo").GetString();
        Assert.Matches(@"^A-\d{4}$", codigo);

        var devolucion = await Json(await Escanear(operador, "CREO-T05"));   // por el contenido del QR
        Assert.Equal("DEVOLUCION", devolucion.GetProperty("operacion").GetString());
        Assert.Equal("POR_DESINFECTAR", devolucion.GetProperty("tarjeta").GetProperty("estado").GetString());

        var id = devolucion.GetProperty("tarjeta").GetProperty("id").GetInt32();
        var desinfeccion = await operador.PostAsync($"/api/v1/tarjetas/{id}/desinfectar", null);
        Assert.Equal(HttpStatusCode.OK, desinfeccion.StatusCode);

        // Lo que quedó en la base: estado, columnas de registro (ADR-18) y auditoría (RN-27)
        var (estado, cambiadoPor, acciones) = await api.EnBaseAsync(async db =>
        {
            var t = await db.Tarjetas.SingleAsync(x => x.Codigo == TarjetasDePrueba.CicloCompleto);
            var operadorId = await db.Usuarios.Where(u => u.Correo == "operador@creo.pe").Select(u => u.Id).SingleAsync();
            var acc = await db.Auditoria.Where(a => a.UsuarioRegistroId == operadorId)
                .Select(a => a.AccionAuditoriaId).ToListAsync();
            return (t.EstadoTarjetaId, t.UsuarioActualizacionId == operadorId, acc);
        });
        Assert.Equal(EstadoTarjeta.Disponible, estado);
        Assert.True(cambiadoPor);
        Assert.Contains(AccionAuditoria.Entregar, acciones);
        Assert.Contains(AccionAuditoria.Recibir, acciones);
        Assert.Contains(AccionAuditoria.Desinfectar, acciones);
    }

    [Fact]
    public async Task El_resumen_no_muestra_el_alta_de_tarjetas_como_operacion()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");

        var resumen = await Json(await operador.GetAsync("/api/v1/admision/resumen"));

        // La semilla da de alta 15 tarjetas: eso es inventario, no actividad del mostrador
        Assert.DoesNotContain(resumen.GetProperty("ultimasOperaciones").EnumerateArray(),
            o => o.GetProperty("tipo").GetString() == "REGISTRADA");
    }

    [Fact]
    public async Task Vista_previa_dice_que_pasaria_sin_registrar_nada()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");

        // Por el contenido del QR impreso en la tarjeta, igual que el lector
        var previa = await Json(await operador.GetAsync("/api/v1/admision/vista-previa?qr=CREO-T13"));
        Assert.Equal("T-13", previa.GetProperty("tarjeta").GetString());
        Assert.Equal("ENTREGA", previa.GetProperty("operacion").GetString());
        Assert.Equal(JsonValueKind.String, previa.GetProperty("desde").ValueKind);   // desde cuándo está disponible

        // Mirar no cambia nada: la tarjeta sigue disponible y no nació ninguna atención
        var (estado, atenciones) = await api.EnBaseAsync(async db =>
        {
            var t = await db.Tarjetas.SingleAsync(x => x.Codigo == TarjetasDePrueba.VistaPrevia);
            return (t.EstadoTarjetaId, await db.Atenciones.CountAsync(a => a.TarjetaId == t.Id));
        });
        Assert.Equal(EstadoTarjeta.Disponible, estado);
        Assert.Equal(0, atenciones);
    }

    [Fact]
    public async Task Vista_previa_avisa_la_regla_que_impediria_el_registro()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");
        await DejarPorDesinfectarAsync(operador, TarjetasDePrueba.PorDesinfectar);

        var previa = await Json(await operador.GetAsync($"/api/v1/admision/vista-previa?qr={TarjetasDePrueba.PorDesinfectar}"));

        Assert.Equal(JsonValueKind.Null, previa.GetProperty("operacion").ValueKind);
        Assert.Equal("RN03_POR_DESINFECTAR", previa.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Vista_previa_de_tarjeta_inexistente_responde_404()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");

        var r = await operador.GetAsync("/api/v1/admision/vista-previa?qr=T-80");

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal("TARJETA_NO_REGISTRADA", (await Json(r)).GetProperty("codigo").GetString());
    }

    private static async Task<int> IdDeAsync(HttpClient c, string codigo) =>
        (await Json(await c.GetAsync($"/api/v1/admision/vista-previa?qr={codigo}"))).GetProperty("tarjetaId").GetInt32();

    [Fact]
    public async Task Desinfectar_por_tandas_cambia_todas_y_deja_su_auditoria()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");
        await DejarPorDesinfectarAsync(operador, TarjetasDePrueba.TandaA);
        await DejarPorDesinfectarAsync(operador, TarjetasDePrueba.TandaB);
        int[] ids = [await IdDeAsync(operador, TarjetasDePrueba.TandaA), await IdDeAsync(operador, TarjetasDePrueba.TandaB)];

        var r = await operador.PostAsJsonAsync("/api/v1/tarjetas/desinfectar", new { tarjetaIds = ids });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.All((await Json(r)).EnumerateArray(), t => Assert.Equal("DISPONIBLE", t.GetProperty("estado").GetString()));
        var auditadas = await api.EnBaseAsync(db => db.Auditoria
            .CountAsync(a => a.AccionAuditoriaId == AccionAuditoria.Desinfectar
                && (a.EntidadId == TarjetasDePrueba.TandaA || a.EntidadId == TarjetasDePrueba.TandaB)));
        Assert.True(auditadas >= 2);
    }

    [Fact]
    public async Task Una_tanda_con_una_tarjeta_que_no_corresponde_no_cambia_ninguna()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");
        await DejarPorDesinfectarAsync(operador, TarjetasDePrueba.TandaPendiente);
        int[] ids = [await IdDeAsync(operador, TarjetasDePrueba.TandaPendiente), await IdDeAsync(operador, TarjetasDePrueba.TandaDisponible)];

        var r = await operador.PostAsJsonAsync("/api/v1/tarjetas/desinfectar", new { tarjetaIds = ids });

        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Contains(TarjetasDePrueba.TandaDisponible, (await Json(r)).GetProperty("title").GetString());
        // Todas o ninguna: la que sí estaba por desinfectar sigue igual
        var previa = await Json(await operador.GetAsync($"/api/v1/admision/vista-previa?qr={TarjetasDePrueba.TandaPendiente}"));
        Assert.Equal("POR_DESINFECTAR", previa.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Una_tanda_vacia_se_rechaza_con_mensaje_claro()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");

        var r = await operador.PostAsJsonAsync("/api/v1/tarjetas/desinfectar", new { tarjetaIds = Array.Empty<int>() });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("Marca al menos una tarjeta.", (await r.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Deshacer_la_entrega_anula_la_atencion_y_libera_la_tarjeta()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");
        var entrega = await Json(await Escanear(operador, TarjetasDePrueba.Deshacer));
        var atencionId = entrega.GetProperty("atencion").GetProperty("id").GetInt64();

        var r = await operador.PostAsync($"/api/v1/admision/atenciones/{atencionId}/deshacer", null);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("DISPONIBLE", (await Json(r)).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Tarjeta_por_desinfectar_no_se_entrega()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");
        await DejarPorDesinfectarAsync(operador, TarjetasDePrueba.PorDesinfectar);

        var r = await Escanear(operador, TarjetasDePrueba.PorDesinfectar);

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("RN03_POR_DESINFECTAR", (await Json(r)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Tarjeta_inexistente_responde_404_con_mensaje_claro()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");

        var r = await Escanear(operador, "T-80");

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal("TARJETA_NO_REGISTRADA", (await Json(r)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Gerencia_no_puede_escribir_RN25()
    {
        var gerencia = await api.ClienteDeAsync("gerencia@creo.pe");

        var r = await Escanear(gerencia, TarjetasDePrueba.SinPermiso);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal("SIN_PERMISO", (await Json(r)).GetProperty("codigo").GetString());
    }
}
