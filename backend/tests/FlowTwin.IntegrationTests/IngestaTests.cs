using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTwin.Api.Infraestructura;
using FlowTwin.Atenciones.Application;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Application;
using FlowTwin.Dispositivos.Domain;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.IntegrationTests;

/// <summary>
/// La puerta de entrada de los ESP32: autenticación del nodo, latidos (F-15), lecturas (F-07),
/// vigilante (RN-20) y purga (DD-11). Usa los nodos de la semilla: N-01 para lecturas y N-03 para
/// latidos (N-02 lo deja Sin señal la prueba de geometría).
/// </summary>
[Collection("Api")]
public class IngestaTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private HttpClient Nodo(string? clave)
    {
        var c = api.Fabrica.CreateClient();
        if (clave is not null) c.DefaultRequestHeaders.Add("X-Node-Key", clave);
        return c;
    }

    private static object Lote(string mac, long secuencia, params (string uuid, int rssi, int edadMs)[] lecturas) =>
        new { mac, secuencia, lecturas = lecturas.Select(l => new { uuidBle = l.uuid, l.rssi, l.edadMs }) };

    [Fact]
    public async Task El_primer_latido_pone_al_nodo_en_linea_y_queda_en_su_historial()
    {
        var r = await Nodo(SemillaDesarrollo.ClaveNodoDemo("N-03"))
            .PostAsJsonAsync("/api/v1/ingesta/latidos", new { mac = "02:00:00:00:00:03", versionFirmware = "1.0.0", rssiWifi = -55 });

        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        var (estado, firmware, latido, eventos) = await api.EnBaseAsync(async db =>
        {
            var n = await db.Nodos.SingleAsync(x => x.Codigo == "N-03");
            return (n.EstadoNodoId, n.VersionFirmware, n.UltimoLatidoEn,
                await db.EventosNodo.CountAsync(e => e.NodoId == n.Id && e.EstadoNodoId == EstadoNodo.EnLinea));
        });
        Assert.Equal(EstadoNodo.EnLinea, estado);
        Assert.Equal("1.0.0", firmware);
        Assert.NotNull(latido);
        Assert.True(eventos >= 1);
    }

    [Theory]
    [InlineData("nk_clave-inventada", "02:00:00:00:00:01")]          // clave incorrecta
    [InlineData(null, "02:00:00:00:00:01")]                          // sin cabecera
    [InlineData("nk_dev_N-01", "02:00:00:00:00:02")]                 // clave de N-01 con la MAC de N-02
    public async Task Sin_clave_valida_para_esa_MAC_responde_401_y_no_guarda_nada(string? clave, string mac)
    {
        var antes = await api.EnBaseAsync(db => db.LecturasBle.CountAsync());

        var r = await Nodo(clave).PostAsJsonAsync("/api/v1/ingesta/lecturas", Lote(mac, 1, ("uuid-x", -60, 100)));

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal(antes, await api.EnBaseAsync(db => db.LecturasBle.CountAsync()));
    }

    [Fact]
    public async Task Un_lote_valido_guarda_las_lecturas_con_la_hora_del_servidor()
    {
        var uuid = $"f7826da6-prueba-{Guid.NewGuid():N}"[..40];
        var antes = DateTime.UtcNow;

        var r = await Nodo(SemillaDesarrollo.ClaveNodoDemo("N-01"))
            .PostAsJsonAsync("/api/v1/ingesta/lecturas", Lote("02:00:00:00:00:01", 1001, (uuid.ToUpperInvariant(), -61, 2_000)));

        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        Assert.Equal(1, (await Json(r)).GetProperty("recibidas").GetInt32());
        var lectura = await api.EnBaseAsync(db => db.LecturasBle.SingleAsync(l => l.UuidBle == uuid));
        Assert.Equal(-61, lectura.Rssi);
        // LeidaEn ≈ llegada - 2 s, calculada con el reloj del servidor
        Assert.InRange(lectura.LeidaEn, antes.AddSeconds(-3), DateTime.UtcNow.AddSeconds(-1));
    }

    [Fact]
    public async Task Un_lote_reenviado_no_se_guarda_dos_veces()
    {
        var uuid = $"reenvio-{Guid.NewGuid():N}";
        var nodo = Nodo(SemillaDesarrollo.ClaveNodoDemo("N-01"));
        var lote = Lote("02:00:00:00:00:01", 2002, (uuid, -70, 500));

        var primero = await Json(await nodo.PostAsJsonAsync("/api/v1/ingesta/lecturas", lote));
        var segundo = await Json(await nodo.PostAsJsonAsync("/api/v1/ingesta/lecturas", lote));

        Assert.False(primero.GetProperty("duplicado").GetBoolean());
        Assert.True(segundo.GetProperty("duplicado").GetBoolean());
        Assert.Equal(1, await api.EnBaseAsync(db => db.LecturasBle.CountAsync(l => l.UuidBle == uuid)));
    }

    [Fact]
    public async Task Una_lectura_con_RSSI_imposible_responde_400()
    {
        var r = await Nodo(SemillaDesarrollo.ClaveNodoDemo("N-01"))
            .PostAsJsonAsync("/api/v1/ingesta/lecturas", Lote("02:00:00:00:00:01", 3003, ("uuid-y", 12, 100)));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Al_superar_el_limite_responde_429_con_Retry_After()
    {
        using var app = api.Fabrica.WithWebHostBuilder(b => b.UseSetting("LimitePeticiones:IngestaPorMinuto", "1"));
        var nodo = app.CreateClient();
        nodo.DefaultRequestHeaders.Add("X-Node-Key", SemillaDesarrollo.ClaveNodoDemo("N-01"));
        var latido = new { mac = "02:00:00:00:00:01" };

        await nodo.PostAsJsonAsync("/api/v1/ingesta/latidos", latido);
        var r = await nodo.PostAsJsonAsync("/api/v1/ingesta/latidos", latido);

        Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
        Assert.True(int.Parse(r.Headers.GetValues("Retry-After").Single()) > 0);   // el ESP32 sabe cuánto esperar
    }

    [Fact]
    public async Task Un_nodo_de_baja_ya_no_puede_enviar_aunque_tenga_su_clave()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var creado = await Json(await admin.PostAsJsonAsync("/api/v1/nodos",
            new { codigo = "N-60", mac = "A4:CF:12:9B:3E:60", zonaId = 3, posX = 20.0, posY = 16.0 }));
        var clave = creado.GetProperty("clave").GetString()!;
        await admin.PostAsync($"/api/v1/nodos/{creado.GetProperty("nodo").GetProperty("id").GetInt32()}/baja", null);

        var r = await Nodo(clave).PostAsJsonAsync("/api/v1/ingesta/latidos", new { mac = "A4:CF:12:9B:3E:60" });

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task El_vigilante_pasa_a_Sin_senal_un_nodo_callado_RN20()
    {
        // Un nodo En línea cuyo último latido fue hace 5 minutos
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var id = (await Json(await admin.PostAsJsonAsync("/api/v1/nodos",
            new { codigo = "N-61", mac = "A4:CF:12:9B:3E:61", zonaId = 4, posX = 26.0, posY = 15.0 })))
            .GetProperty("nodo").GetProperty("id").GetInt32();
        await api.EnBaseAsync(async db =>
        {
            var n = await db.Nodos.SingleAsync(x => x.Id == id);
            (n.EstadoNodoId, n.UltimoLatidoEn) = (EstadoNodo.EnLinea, DateTime.UtcNow.AddMinutes(-5));
            return await db.SaveChangesAsync();
        });

        using var scope = api.Fabrica.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IngestaNodosServicio>().RevisarLatidosAsync(CancellationToken.None);

        var (estado, detalle) = await api.EnBaseAsync(async db => (
            await db.Nodos.Where(n => n.Id == id).Select(n => n.EstadoNodoId).SingleAsync(),
            await db.EventosNodo.Where(e => e.NodoId == id).OrderByDescending(e => e.Id).Select(e => e.Detalle).FirstAsync()));
        Assert.Equal(EstadoNodo.SinSenal, estado);
        Assert.StartsWith("Sin latido desde", detalle);
    }

    [Fact]
    public async Task La_purga_borra_solo_las_lecturas_de_mas_de_7_dias_DD11()
    {
        var vieja = $"purga-vieja-{Guid.NewGuid():N}";
        var nueva = $"purga-nueva-{Guid.NewGuid():N}";
        await api.EnBaseAsync(async db =>
        {
            db.LecturasBle.AddRange(
                LecturaBle.Desde(1, vieja, -60, 0, DateTime.UtcNow.AddDays(-8)),
                LecturaBle.Desde(1, nueva, -60, 0, DateTime.UtcNow.AddDays(-6)));
            return await db.SaveChangesAsync();
        });

        using var scope = api.Fabrica.Services.CreateScope();
        var borradas = await scope.ServiceProvider.GetRequiredService<PurgaLecturasServicio>().PurgarAsync(CancellationToken.None);

        Assert.True(borradas >= 1);
        Assert.False(await api.EnBaseAsync(db => db.LecturasBle.AnyAsync(l => l.UuidBle == vieja)));
        Assert.True(await api.EnBaseAsync(db => db.LecturasBle.AnyAsync(l => l.UuidBle == nueva)));
    }
}
