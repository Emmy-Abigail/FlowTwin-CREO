using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FlowTwin.IntegrationTests;

/// <summary>Configuración (ADR-27): sedes, pisos y zonas como datos, y los parámetros del sistema. Solo el Administrador.</summary>
[Collection("Api")]
public class ConfiguracionGemeloTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Solo_el_Administrador_entra_a_Configuracion()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");

        Assert.Equal(HttpStatusCode.Forbidden, (await operador.GetAsync("/api/v1/configuracion/arbol")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.GetAsync("/api/v1/configuracion/parametros")).StatusCode);
    }

    [Fact]
    public async Task Una_sede_nueva_con_su_piso_y_sus_zonas_se_publica_y_aparece_en_el_arbol()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");

        var sede = await Json(await admin.PostAsJsonAsync("/api/v1/configuracion/sedes", new { nombre = "Sede Norte (prueba)", direccion = "Av. Prueba 123", prefijoAtencion = 'Q' }));
        var sedeId = sede.GetProperty("id").GetInt32();
        var piso = await Json(await admin.PostAsJsonAsync($"/api/v1/configuracion/sedes/{sedeId}/pisos", new { nombre = "Piso 1", nivel = 1 }));
        var pisoId = piso.GetProperty("id").GetInt32();
        Assert.False(piso.GetProperty("publicado").GetBoolean());   // nace en borrador

        var sinZonas = await admin.PostAsync($"/api/v1/configuracion/pisos/{pisoId}/publicar", null);
        Assert.Equal("PISO_SIN_ZONAS", (await Json(sinZonas)).GetProperty("codigo").GetString());

        var sala = await Json(await admin.PostAsJsonAsync($"/api/v1/configuracion/pisos/{pisoId}/zonas", new { nombre = "Sala de espera", tipo = "ESPERA", aforo = 20 }));
        var consultorio = await Json(await admin.PostAsJsonAsync($"/api/v1/configuracion/pisos/{pisoId}/zonas", new { nombre = "Consultorio 1", tipo = "SERVICIO", aforo = 1 }));
        Assert.Equal(20, sala.GetProperty("aforo").GetInt32());
        Assert.Equal(1, consultorio.GetProperty("aforo").GetInt32());   // un consultorio: su número de puestos (ADR-27)

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/configuracion/pisos/{pisoId}/publicar", null)).StatusCode);
        var arbol = await Json(await admin.GetAsync("/api/v1/configuracion/arbol"));
        var enArbol = arbol.EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == sedeId);
        Assert.True(enArbol.GetProperty("pisos")[0].GetProperty("publicado").GetBoolean());
        Assert.Equal("Q", enArbol.GetProperty("prefijoAtencion").GetString());
    }

    [Fact]
    public async Task Dos_sedes_no_comparten_la_letra_de_sus_codigos()
    {
        var r = await (await api.ClienteDeAsync("admin@creo.pe"))
            .PostAsJsonAsync("/api/v1/configuracion/sedes", new { nombre = "Otra con A", direccion = (string?)null, prefijoAtencion = 'A' });

        Assert.Equal("SEDE_PREFIJO_DUPLICADO", (await Json(r)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Una_zona_con_nodos_no_se_da_de_baja()
    {
        // Admisión (zona 1) tiene el nodo N-01 de la semilla
        var r = await (await api.ClienteDeAsync("admin@creo.pe")).PostAsync("/api/v1/configuracion/zonas/1/baja", null);

        Assert.Equal("ZONA_CON_NODOS", (await Json(r)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Los_parametros_se_leen_se_guardan_y_se_validan()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var actuales = await Json(await admin.GetAsync("/api/v1/configuracion/parametros"));
        Assert.Equal(80, actuales.GetProperty("semaforoAmbarPct").GetInt32());

        var invalido = await admin.PutAsJsonAsync("/api/v1/configuracion/parametros",
            new { esperaAltaMin = 60, esperaCriticaMin = 40, sinLecturasMin = 30, horaCierre = 22, semaforoAmbarPct = 80 });
        var valido = await admin.PutAsJsonAsync("/api/v1/configuracion/parametros",
            new { esperaAltaMin = 40, esperaCriticaMin = 60, sinLecturasMin = 30, horaCierre = 22, semaforoAmbarPct = 80 });

        Assert.Equal("PARAMETROS_INVALIDOS", (await Json(invalido)).GetProperty("codigo").GetString());
        Assert.Equal(HttpStatusCode.OK, valido.StatusCode);
    }
}
