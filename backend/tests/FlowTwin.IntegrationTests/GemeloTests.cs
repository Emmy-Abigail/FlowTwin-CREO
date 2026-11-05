using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTwin.Gemelo.Domain;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.IntegrationTests;

/// <summary>Geometría del piso (F-11): zonas en la base, permisos por piso (RN-24) y RN-13 en SQL Server.</summary>
[Collection("Api")]
public class GemeloTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task La_geometria_del_Piso_1_trae_sus_8_zonas_desde_la_base()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");

        var r = await operador.GetAsync("/api/v1/pisos/1/geometria");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var piso = await Json(r);
        Assert.Equal(40.12m, piso.GetProperty("anchoM").GetDecimal());
        var zonas = piso.GetProperty("zonas").EnumerateArray().ToList();
        Assert.Equal(8, zonas.Count);

        var sala = zonas.Single(z => z.GetProperty("nombre").GetString() == "Sala de espera");
        Assert.Equal("ESPERA", sala.GetProperty("tipo").GetString());
        Assert.Equal(24, sala.GetProperty("aforo").GetInt32());

        // RN-13: el Hall es Tránsito, no tiene aforo ni semáforo
        var hall = zonas.Single(z => z.GetProperty("tipo").GetString() == "TRANSITO");
        Assert.Equal(JsonValueKind.Null, hall.GetProperty("aforo").ValueKind);
    }

    [Fact]
    public async Task La_geometria_trae_los_20_muros_importados_de_AutoCAD()
    {
        var gerencia = await api.ClienteDeAsync("gerencia@creo.pe");

        var piso = await Json(await gerencia.GetAsync("/api/v1/pisos/1/geometria"));

        var muros = piso.GetProperty("elementos").EnumerateArray().Where(e => e.GetProperty("tipo").GetString() == "MURO").ToList();
        Assert.Equal(20, muros.Count);
        Assert.All(muros, m => Assert.True(m.GetProperty("poligono").GetArrayLength() >= 3));
        // Los vanos de puertas y las columnas viajan como huecos de los polígonos
        Assert.Equal(59, muros.Sum(m => m.GetProperty("huecos").GetArrayLength()));
    }

    [Fact]
    public async Task Un_piso_que_no_existe_responde_404()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");

        var r = await admin.GetAsync("/api/v1/pisos/999/geometria");

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Sin_el_piso_asignado_no_se_ve_su_geometria_RN24()
    {
        // Un segundo piso y un usuario que solo tiene ese piso
        await api.EnBaseAsync(async db =>
        {
            db.Pisos.Add(new Piso { SedeId = 1, Nombre = "Piso 2 (prueba)", Nivel = 2, Publicado = true });
            return await db.SaveChangesAsync();
        });
        var piso2 = await api.EnBaseAsync(db => db.Pisos.Where(p => p.Nivel == 2).Select(p => p.Id).SingleAsync());
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var creado = await Json(await admin.PostAsJsonAsync("/api/v1/usuarios",
            new { nombres = "Lucía", apellidos = "Paredes", correo = "lucia.paredes@creo.pe", rol = "OPERADOR", pisos = new[] { piso2 } }));
        var temporal = creado.GetProperty("contrasenaTemporal").GetString()!;

        // Sin la regla F-02, para entrar directo con la temporal
        using var app = api.Fabrica.WithWebHostBuilder(b => b.UseSetting("Contrasenas:ExigirCambioPrimerIngreso", "false"));
        var lucia = app.CreateClient();
        var login = await Json(await lucia.PostAsJsonAsync("/api/v1/auth/login", new { correo = "lucia.paredes@creo.pe", clave = temporal }));
        lucia.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());

        var r = await lucia.GetAsync("/api/v1/pisos/1/geometria");

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal("SIN_PERMISO", (await Json(r)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task La_base_rechaza_un_hall_con_aforo_aunque_no_pase_por_el_dominio_RN13()
    {
        // Defensa en profundidad: se salta Zona.Validar() a propósito y la restricción CHECK lo frena igual
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => api.EnBaseAsync(async db =>
        {
            db.Zonas.Add(new Zona
            {
                PisoId = 1, TipoZonaId = TipoZona.Transito, Nombre = "Pasillo (prueba)",
                X = 1, Y = 1, AnchoM = 2, FondoM = 2, Aforo = 10,
            });
            return await db.SaveChangesAsync();
        }));

        Assert.Contains("CK_Zona_AforoRN13", ex.InnerException?.Message);
    }
}
