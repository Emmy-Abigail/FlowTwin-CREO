using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTwin.Api.Infraestructura;
using FlowTwin.Atenciones.Application;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Application;
using FlowTwin.Dispositivos.Domain;
using MQTTnet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.IntegrationTests;

/// <summary>
/// La puerta de entrada de los ESP32 por MQTT (ADR-24): cuentas del broker, latidos (F-15), lecturas (F-07),
/// vigilante (RN-20) y purga (DD-11). Un «nodo de prueba» se conecta al Mosquitto real con su usuario y
/// clave. Usa los nodos de la semilla: N-01 para lecturas y N-03 para latidos (N-02 lo deja Sin señal la
/// prueba de geometría).
/// </summary>
[Collection("Api")]
public class IngestaTests(ApiFixture api)
{
    private const string MacN01 = "02:00:00:00:00:01", MacN02 = "02:00:00:00:00:02", MacN03 = "02:00:00:00:00:03";

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private Task<NodoMqtt> N01() => NodoMqtt.ConectarCuandoExistaAsync(MacN01, SemillaDesarrollo.ClaveNodoDemo("N-01"));

    [Fact]
    public async Task El_primer_latido_pone_al_nodo_en_linea_y_queda_en_su_historial()
    {
        await using var nodo = await NodoMqtt.ConectarCuandoExistaAsync(MacN03, SemillaDesarrollo.ClaveNodoDemo("N-03"));

        var r = await nodo.EnviarAsync("latidos", new { versionFirmware = "1.0.0", rssiWifi = -55 });

        Assert.Equal("GUARDADO", r.GetProperty("estado").GetString());
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

    [Fact]
    public async Task Con_una_clave_incorrecta_el_broker_no_lo_deja_conectarse()
    {
        await using var _ = await N01();   // la cuenta ya existe: el rechazo es por la clave, no porque falte

        var e = await Assert.ThrowsAsync<ConexionRechazadaException>(() => NodoMqtt.ConectarAsync(MacN01, "nk_clave-inventada"));

        // MQTT 3.1.1 solo tiene «no autorizado»: no dice si falló el usuario o la clave, y así debe ser
        Assert.Equal(MqttClientConnectResultCode.NotAuthorized, e.Codigo);
    }

    [Fact]
    public async Task Un_nodo_no_puede_publicar_en_el_tema_de_otro()
    {
        await using var n01 = await N01();
        var uuid = $"suplantacion-{Guid.NewGuid():N}";

        // N-01 intenta enviar lecturas como si fuera N-02: el rol del broker lo impide
        await n01.PublicarAsync("lecturas", NodoMqtt.Lote(1, (uuid, -60, 100)),
            raiz: $"{ApiFixture.PrefijoMqtt}/v1/nodos/{MacN02.Replace(":", "")}");

        Assert.Null(await n01.RespuestaAsync(TimeSpan.FromSeconds(2)));
        Assert.False(await api.EnBaseAsync(db => db.LecturasBle.AnyAsync(l => l.UuidBle == uuid)));
    }

    [Fact]
    public async Task Un_lote_valido_se_guarda_con_la_hora_del_servidor_y_se_confirma()
    {
        await using var nodo = await N01();
        var uuid = $"f7826da6-prueba-{Guid.NewGuid():N}"[..40];
        var antes = DateTime.UtcNow;

        var r = await nodo.EnviarAsync("lecturas", NodoMqtt.Lote(1001, (uuid.ToUpperInvariant(), -61, 2_000)));

        Assert.Equal("GUARDADO", r.GetProperty("estado").GetString());
        Assert.Equal(1001, r.GetProperty("secuencia").GetInt64());   // el nodo sabe qué lote borrar de su búfer
        var lectura = await api.EnBaseAsync(db => db.LecturasBle.SingleAsync(l => l.UuidBle == uuid));
        Assert.Equal(-61, lectura.Rssi);
        // LeidaEn ≈ llegada - 2 s, calculada con el reloj del servidor
        Assert.InRange(lectura.LeidaEn, antes.AddSeconds(-3), DateTime.UtcNow.AddSeconds(-1));
    }

    [Fact]
    public async Task Un_lote_reenviado_se_confirma_como_duplicado_y_no_se_guarda_dos_veces()
    {
        await using var nodo = await N01();
        var uuid = $"reenvio-{Guid.NewGuid():N}";
        var lote = NodoMqtt.Lote(2002, (uuid, -70, 500));

        var primero = await nodo.EnviarAsync("lecturas", lote);
        var segundo = await nodo.EnviarAsync("lecturas", lote);

        Assert.Equal("GUARDADO", primero.GetProperty("estado").GetString());
        Assert.Equal("DUPLICADO", segundo.GetProperty("estado").GetString());
        Assert.Equal(1, await api.EnBaseAsync(db => db.LecturasBle.CountAsync(l => l.UuidBle == uuid)));
    }

    [Fact]
    public async Task Un_lote_que_no_cumple_el_contrato_responde_invalido_con_sus_errores()
    {
        await using var nodo = await N01();

        var rssi = await nodo.EnviarAsync("lecturas", NodoMqtt.Lote(3003, ("uuid-y", 12, 100)));
        await nodo.PublicarCrudoAsync("lecturas", "esto no es json");
        var roto = await nodo.RespuestaAsync();

        Assert.Equal("INVALIDO", rssi.GetProperty("estado").GetString());
        Assert.Equal(3003, rssi.GetProperty("secuencia").GetInt64());
        Assert.True(rssi.GetProperty("errores").EnumerateObject().Any());
        Assert.Equal("INVALIDO", roto?.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Registrar_un_nodo_le_crea_su_cuenta_y_regenerar_la_clave_corta_la_anterior()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var creado = await Json(await admin.PostAsJsonAsync("/api/v1/nodos",
            new { codigo = "N-62", mac = "A4:CF:12:9B:3E:62", zonaId = 3, posX = 20.0, posY = 16.0 }));
        var id = creado.GetProperty("nodo").GetProperty("id").GetInt32();
        var mqtt = creado.GetProperty("mqtt");
        Assert.Equal(NodoMqtt.Usuario("A4:CF:12:9B:3E:62"), mqtt.GetProperty("usuario").GetString());
        Assert.EndsWith("/a4cf129b3e62/lecturas", mqtt.GetProperty("temaLecturas").GetString());

        await using var conClaveVieja = await NodoMqtt.ConectarAsync("A4:CF:12:9B:3E:62", creado.GetProperty("clave").GetString()!);
        Assert.Equal("GUARDADO", (await conClaveVieja.EnviarAsync("latidos", new { })).GetProperty("estado").GetString());

        var nueva = (await Json(await admin.PostAsync($"/api/v1/nodos/{id}/regenerar-clave", null))).GetProperty("clave").GetString()!;

        await Task.Delay(500);
        Assert.False(conClaveVieja.Conectado);   // el broker cerró la sesión de la clave anterior
        await Assert.ThrowsAsync<ConexionRechazadaException>(() => NodoMqtt.ConectarAsync("A4:CF:12:9B:3E:62", creado.GetProperty("clave").GetString()!));
        await using var conClaveNueva = await NodoMqtt.ConectarAsync("A4:CF:12:9B:3E:62", nueva);
        Assert.True(conClaveNueva.Conectado);
    }

    [Fact]
    public async Task Un_nodo_de_baja_queda_desconectado_y_ya_no_puede_entrar()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var creado = await Json(await admin.PostAsJsonAsync("/api/v1/nodos",
            new { codigo = "N-60", mac = "A4:CF:12:9B:3E:60", zonaId = 3, posX = 20.0, posY = 16.0 }));
        var clave = creado.GetProperty("clave").GetString()!;
        await using var nodo = await NodoMqtt.ConectarAsync("A4:CF:12:9B:3E:60", clave);

        await admin.PostAsync($"/api/v1/nodos/{creado.GetProperty("nodo").GetProperty("id").GetInt32()}/baja", null);

        await Task.Delay(500);
        Assert.False(nodo.Conectado);
        await Assert.ThrowsAsync<ConexionRechazadaException>(() => NodoMqtt.ConectarAsync("A4:CF:12:9B:3E:60", clave));
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
