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

        var r = await Escanear(operador, "T-11");

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
