using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.IntegrationTests;

/// <summary>Flujos F-01, F-02, F-04 y F-05, y el borrado lógico que pidió el patrocinador (RN-28, RN-29).</summary>
[Collection("Api")]
public class IdentidadTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Login_correcto_devuelve_token_y_cookie_de_refresco_HttpOnly()
    {
        var r = await api.Fabrica.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { correo = "admin@creo.pe", clave = "demo123" });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var cuerpo = await Json(r);
        Assert.False(string.IsNullOrEmpty(cuerpo.GetProperty("accessToken").GetString()));
        Assert.Equal("ADMIN", cuerpo.GetProperty("usuario").GetProperty("rol").GetString());
        var cookie = Assert.Single(r.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_incorrecto_da_un_mensaje_generico()
    {
        var r = await api.Fabrica.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { correo = "noexiste@creo.pe", clave = "x" });

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal("Correo o contraseña incorrectos.", (await Json(r)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Primer_ingreso_bloquea_todo_hasta_cambiar_la_contrasena()
    {
        var nuevo = await api.ClienteDeAsync("nuevo@creo.pe");

        var antes = await nuevo.GetAsync("/api/v1/sedes");
        Assert.Equal(HttpStatusCode.Forbidden, antes.StatusCode);
        Assert.Equal("CAMBIO_CONTRASENA_REQUERIDO", (await Json(antes)).GetProperty("codigo").GetString());

        var cambio = await nuevo.PostAsJsonAsync("/api/v1/auth/cambiar-contrasena",
            new { actual = "demo123", nueva = "Admision#2026", confirmacion = "Admision#2026" });
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);
        Assert.False((await Json(cambio)).GetProperty("usuario").GetProperty("debeCambiarContrasena").GetBoolean());
    }

    [Fact]
    public async Task Con_la_regla_F02_apagada_la_temporal_entra_directo()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var creado = await Json(await admin.PostAsJsonAsync("/api/v1/usuarios",
            new { nombres = "Marta", apellidos = "Quispe", correo = "marta.quispe@creo.pe", rol = "OPERADOR", pisos = new[] { 1 } }));
        var temporal = creado.GetProperty("contrasenaTemporal").GetString()!;

        using var sinRegla = api.ConAjustes(("Contrasenas:ExigirCambioPrimerIngreso", "false"));
        var cliente = sinRegla.CreateClient();
        var login = await Json(await cliente.PostAsJsonAsync("/api/v1/auth/login", new { correo = "marta.quispe@creo.pe", clave = temporal }));

        Assert.False(login.GetProperty("usuario").GetProperty("debeCambiarContrasena").GetBoolean());
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/sedes")).StatusCode);
    }

    /// <summary>ADR-29: el Operador necesita pisos; Gerencia y Administrador ven todos y no guardan ninguno.</summary>
    [Fact]
    public async Task Solo_el_Operador_tiene_pisos_asignados_y_Gerencia_ve_todos_ADR29()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");

        var operadorSinPisos = await admin.PostAsJsonAsync("/api/v1/usuarios",
            new { nombres = "Sin", apellidos = "Pisos", correo = "sin.pisos@creo.pe", rol = "OPERADOR", pisos = Array.Empty<int>() });
        var gerente = await admin.PostAsJsonAsync("/api/v1/usuarios",
            new { nombres = "Gloria", apellidos = "Paz", correo = "gloria.paz@creo.pe", rol = "GERENCIA", pisos = new[] { 1 } });   // los pisos se ignoran

        Assert.Equal("RN24_PISO_REQUERIDO", (await Json(operadorSinPisos)).GetProperty("codigo").GetString());
        Assert.Equal(HttpStatusCode.Created, gerente.StatusCode);
        var cuerpo = await Json(gerente);
        Assert.Empty(cuerpo.GetProperty("usuario").GetProperty("pisos").EnumerateArray());

        // Entra y ve los pisos publicados aunque no tenga ninguno asignado; sigue siendo de solo lectura
        using var sinRegla = api.ConAjustes(("Contrasenas:ExigirCambioPrimerIngreso", "false"));
        var cliente = sinRegla.CreateClient();
        var login = await Json(await cliente.PostAsJsonAsync("/api/v1/auth/login",
            new { correo = "gloria.paz@creo.pe", clave = cuerpo.GetProperty("contrasenaTemporal").GetString() }));
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        var sedes = await Json(await cliente.GetAsync("/api/v1/sedes"));
        Assert.NotEmpty(sedes.EnumerateArray().SelectMany(sede => sede.GetProperty("pisos").EnumerateArray()));
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/pisos/1/estado")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/v1/pisos/1/alertas")).StatusCode);   // acciones del Operador: no
    }

    [Fact]
    public async Task Pasar_de_Operador_a_Gerencia_da_de_baja_sus_pisos_ADR29()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var creado = await Json(await admin.PostAsJsonAsync("/api/v1/usuarios",
            new { nombres = "Pablo", apellidos = "Cruz", correo = "pablo.cruz@creo.pe", rol = "OPERADOR", pisos = new[] { 1 } }));
        var id = creado.GetProperty("usuario").GetProperty("id").GetInt32();
        Assert.Equal([1], creado.GetProperty("usuario").GetProperty("pisos").EnumerateArray().Select(p => p.GetInt32()));

        var editado = await Json(await admin.PutAsJsonAsync($"/api/v1/usuarios/{id}",
            new { nombres = "Pablo", apellidos = "Cruz", correo = "pablo.cruz@creo.pe", rol = "GERENCIA", pisos = Array.Empty<int>() }));

        Assert.Empty(editado.GetProperty("pisos").EnumerateArray());
    }

    [Fact]
    public async Task Crear_y_dar_de_baja_un_usuario_no_lo_borra_RN28()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");

        var creado = await admin.PostAsJsonAsync("/api/v1/usuarios",
            new { nombres = "Rosa", apellidos = "Vega", correo = "rosa.vega@creo.pe", rol = "OPERADOR", pisos = new[] { 1 } });
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var cuerpo = await Json(creado);
        Assert.Equal(12, cuerpo.GetProperty("contrasenaTemporal").GetString()!.Length);
        var id = cuerpo.GetProperty("usuario").GetProperty("id").GetInt32();

        var baja = await admin.PostAsync($"/api/v1/usuarios/{id}/desactivar", null);
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        // Desaparece del listado normal, pero sigue en la base y se ve con "incluir inactivos" (RN-29)
        var activos = await Json(await admin.GetAsync("/api/v1/usuarios"));
        Assert.DoesNotContain(activos.EnumerateArray(), u => u.GetProperty("id").GetInt32() == id);
        var todos = await Json(await admin.GetAsync("/api/v1/usuarios?incluirInactivos=true"));
        var rosa = Assert.Single(todos.EnumerateArray(), u => u.GetProperty("id").GetInt32() == id);
        Assert.False(rosa.GetProperty("estadoRegistro").GetBoolean());

        var fila = await api.EnBaseAsync(db => db.Usuarios.IgnoreQueryFilters().SingleAsync(u => u.Id == id));
        Assert.False(fila.EstadoRegistro);
        Assert.NotNull(fila.FechaActualizacion);
        Assert.NotNull(fila.UsuarioActualizacionId);
    }

    [Fact]
    public async Task No_se_puede_desactivar_al_unico_administrador_RN23()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var adminId = (await Json(await admin.GetAsync("/api/v1/auth/yo"))).GetProperty("id").GetInt32();

        var r = await admin.PostAsync($"/api/v1/usuarios/{adminId}/desactivar", null);

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("RN23_AUTODESACTIVACION", (await Json(r)).GetProperty("codigo").GetString());
    }
}
