using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FlowTwin.Atenciones.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.IntegrationTests;

/// <summary>
/// GET /pisos/{id}/reportes/... (P5). Las atenciones se arman cerradas directo en la base, en una fecha
/// pasada propia (hace 20 días), para que las demás pruebas, que trabajan con «hoy», no cambien los conteos.
/// </summary>
[Collection("Api")]
public class HistorialTests(ApiFixture api)
{
    private const int Piso1 = 1, Admision = 1, Sala = 3, Modulo2 = 7;
    private static readonly ZonaHoraria Lima = new("America/Lima");
    private static readonly DateOnly Fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)).AddDays(-20);
    private static readonly SemaphoreSlim Una = new(1, 1);
    private static bool sembrado;

    private static string Ruta(string recurso, DateOnly? desde = null, DateOnly? hasta = null, string extra = "") =>
        $"/api/v1/pisos/{Piso1}/reportes/{recurso}?desde={desde ?? Fecha:yyyy-MM-dd}&hasta={hasta ?? Fecha:yyyy-MM-dd}{extra}";

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>
    /// Tres atenciones devueltas ese día, entregadas a las 10:00, 10:10 y 10:20 de Lima:
    /// esperas 10, 20 y 30 min (en la Sala), consultas de 10 min y totales 22, 32 y 42 min (la última sale a las 11:02).
    /// </summary>
    private Task SembrarAsync() => api.EnBaseAsync(async db =>
    {
        await Una.WaitAsync();
        try
        {
            if (sembrado) return 0;
            var tarjeta = await db.Tarjetas.SingleAsync(t => t.Codigo == "T-12");
            var minutos = new[] { 10, 20, 30 };
            for (var i = 0; i < minutos.Length; i++)
            {
                var inicio = Lima.AUtc(Fecha, 10).AddMinutes(10 * i);
                var espera = minutos[i];
                var a = Atencion.Abrir($"A-97{i:D2}", Fecha, tarjeta.SedeId, tarjeta.Id, inicio);
                a.Cerrar(MotivoCierreAtencion.Devuelta, inicio.AddMinutes(espera + 12));
                db.Atenciones.Add(a);
                await db.SaveChangesAsync();
                db.EventosZona.AddRange(
                    new EventoZona { AtencionId = a.Id, ZonaId = Admision, EntradaEn = inicio, SalidaEn = inicio.AddMinutes(2) },
                    new EventoZona { AtencionId = a.Id, ZonaId = Sala, EntradaEn = inicio.AddMinutes(2), SalidaEn = inicio.AddMinutes(espera) },
                    new EventoZona { AtencionId = a.Id, ZonaId = Modulo2, EntradaEn = inicio.AddMinutes(espera), SalidaEn = inicio.AddMinutes(espera + 10) });
            }
            // Una entrega anulada ese día: no cuenta en nada (RN-04)
            var anulada = Atencion.Abrir("A-9799", Fecha, tarjeta.SedeId, tarjeta.Id, Lima.AUtc(Fecha, 11));
            anulada.Cerrar(MotivoCierreAtencion.Anulada, Lima.AUtc(Fecha, 11).AddSeconds(20));
            db.Atenciones.Add(anulada);
            await db.SaveChangesAsync();
            sembrado = true;
            return 0;
        }
        finally { Una.Release(); }
    });

    [Fact]
    public async Task El_resumen_cuenta_las_atenciones_sin_anuladas_y_da_medianas()
    {
        await SembrarAsync();

        var r = await (await api.ClienteDeAsync("operador@creo.pe")).GetAsync(Ruta("resumen"));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var d = await Json(r);
        Assert.Equal(3, d.GetProperty("atenciones").GetInt32());
        Assert.Equal(3, d.GetProperty("terminadas").GetInt32());
        Assert.Equal(20, d.GetProperty("esperaMediana").GetInt32());
        Assert.Equal(30, d.GetProperty("esperaP90").GetInt32());
        Assert.Equal(32, d.GetProperty("totalMediana").GetInt32());
    }

    [Fact]
    public async Task La_tabla_va_de_la_mas_reciente_a_la_mas_antigua_paginada_y_con_busqueda()
    {
        await SembrarAsync();
        var cliente = await api.ClienteDeAsync("gerencia@creo.pe");   // Gerencia también la ve

        var pagina = await Json(await cliente.GetAsync(Ruta("atenciones", extra: "&tamano=2&pagina=1")));
        var busqueda = await Json(await cliente.GetAsync(Ruta("atenciones", extra: "&codigo=a-9701")));

        Assert.Equal(3, pagina.GetProperty("total").GetInt32());
        Assert.Equal(["A-9702", "A-9701"], pagina.GetProperty("filas").EnumerateArray().Select(f => f.GetProperty("codigo").GetString()));
        var fila = busqueda.GetProperty("filas").EnumerateArray().Single();
        Assert.Equal("A-9701", fila.GetProperty("codigo").GetString());
        Assert.Equal((20, 10, 32), (fila.GetProperty("esperaMin").GetInt32(), fila.GetProperty("consultaMin").GetInt32(), fila.GetProperty("totalMin").GetInt32()));
        Assert.True(fila.GetProperty("id").GetInt64() > 0);   // para abrir su detalle (P6)
    }

    [Fact]
    public async Task La_ocupacion_abre_en_la_sala_de_espera_y_sin_nodo_en_linea_es_sin_datos()
    {
        await SembrarAsync();

        var d = await Json(await (await api.ClienteDeAsync("operador@creo.pe")).GetAsync(Ruta("ocupacion")));

        Assert.Equal("ESPERA", d.GetProperty("zonas")[0].GetProperty("tipo").GetString());
        Assert.Equal(Sala, d.GetProperty("zonaId").GetInt32());
        Assert.DoesNotContain(d.GetProperty("zonas").EnumerateArray(), z => z.GetProperty("tipo").GetString() is "TRANSITO" or "SALIDA");
        Assert.Equal("FECHA", d.GetProperty("agrupacion").GetString());
        Assert.Equal((10, 11), (d.GetProperty("horaDesde").GetInt32(), d.GetProperty("horaHasta").GetInt32()));
        // Los nodos de prueba se registraron hoy: hace 20 días la zona no tenía ninguno, así que no se sabe
        // cuánta gente hubo. Se informa «sin datos», nunca un número inventado ni un 0.
        var celda = d.GetProperty("filas")[0].GetProperty("celdas")[0];
        Assert.Equal("SIN_DATOS", celda.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, celda.GetProperty("personas").ValueKind);
    }

    [Fact]
    public async Task El_csv_se_descarga_anonimo_y_queda_en_la_auditoria()
    {
        await SembrarAsync();

        var r = await (await api.ClienteDeAsync("operador@creo.pe")).GetAsync(Ruta("atenciones.csv"));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("text/csv", r.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".csv", r.Content.Headers.ContentDisposition?.FileNameStar ?? r.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var texto = Encoding.UTF8.GetString(await r.Content.ReadAsByteArrayAsync());
        Assert.StartsWith("﻿Código,Fecha", texto);
        Assert.Contains($"A-9700,{Fecha:yyyy-MM-dd},", texto);
        Assert.DoesNotContain("A-9799", texto);   // la anulada no sale
        var auditada = await api.EnBaseAsync(db => db.Auditoria.AnyAsync(a =>
            a.AccionAuditoriaId == AccionAuditoria.Exportar && a.EntidadId == "historial-atenciones" && a.ValorDespues!.Contains(Fecha.ToString("yyyy-MM-dd"))));
        Assert.True(auditada);
    }

    [Fact]
    public async Task Un_periodo_de_mas_de_31_dias_es_400()
    {
        var r = await (await api.ClienteDeAsync("operador@creo.pe")).GetAsync(Ruta("resumen", Fecha.AddDays(-31), Fecha));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("PERIODO_INVALIDO", (await Json(r)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Un_piso_que_no_es_del_usuario_es_403_RN24_y_sin_sesion_401()
    {
        var operador = await (await api.ClienteDeAsync("operador@creo.pe")).GetAsync($"/api/v1/pisos/999/reportes/resumen?desde={Fecha:yyyy-MM-dd}&hasta={Fecha:yyyy-MM-dd}");
        var anonimo = await api.Fabrica.CreateClient().GetAsync(Ruta("resumen"));

        Assert.Equal(HttpStatusCode.Forbidden, operador.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);
    }
}
