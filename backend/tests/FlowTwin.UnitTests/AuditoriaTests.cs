using System.Text;
using FlowTwin.Auditoria.Application;
using FlowTwin.Auditoria.Domain;
using FlowTwin.SharedKernel;

namespace FlowTwin.UnitTests;

/// <summary>P11 · Auditoría: categorías, objeto de cada fila, CSV y la cadena de sellos de integridad.</summary>
public class AuditoriaTests
{
    private static readonly string[] Entidades =
        ["Usuario", "Sede", "Piso", "Zona", "Parametros", "Nodo", "Tarjeta", "Atencion", "Reporte", "Otra"];

    [Fact]
    public void Cada_accion_y_entidad_cae_en_una_sola_categoria_y_SQL_y_C_sharp_coinciden()
    {
        foreach (var accion in Enum.GetValues<AccionAuditoria>())
            foreach (var entidad in Entidades)
            {
                var fila = new RegistroAuditoria { AccionAuditoriaId = accion, Entidad = entidad };
                var esperada = CategoriasAuditoria.De(accion, entidad);
                var quienes = CategoriasAuditoria.Todas.Where(c => CategoriasAuditoria.Predicado(c).Compile()(fila)).ToList();
                Assert.True(quienes.SequenceEqual([esperada]), $"{accion} · {entidad}: C# dice {esperada}, la expresión dice [{string.Join(", ", quienes)}]");
            }
    }

    [Theory]
    [InlineData(AccionAuditoria.IniciarSesion, "Usuario", CategoriasAuditoria.Accesos)]
    [InlineData(AccionAuditoria.InicioSesionFallido, "Usuario", CategoriasAuditoria.Accesos)]
    [InlineData(AccionAuditoria.CambiarContrasena, "Usuario", CategoriasAuditoria.Accesos)]
    [InlineData(AccionAuditoria.RestablecerContrasena, "Usuario", CategoriasAuditoria.Usuarios)]
    [InlineData(AccionAuditoria.Editar, "Zona", CategoriasAuditoria.Configuracion)]
    [InlineData(AccionAuditoria.Pausar, "Zona", CategoriasAuditoria.Operacion)]   // pausar una zona es operación, no configuración
    [InlineData(AccionAuditoria.Editar, "Parametros", CategoriasAuditoria.Configuracion)]
    [InlineData(AccionAuditoria.RegenerarClave, "Nodo", CategoriasAuditoria.Dispositivos)]
    [InlineData(AccionAuditoria.Entregar, "Atencion", CategoriasAuditoria.Operacion)]
    [InlineData(AccionAuditoria.Exportar, "Reporte", CategoriasAuditoria.Operacion)]
    public void Las_categorias_dicen_lo_que_el_Administrador_espera(AccionAuditoria accion, string entidad, string categoria)
    {
        Assert.Equal(categoria, CategoriasAuditoria.De(accion, entidad));
    }

    [Fact]
    public void Una_categoria_desconocida_no_se_acepta()
    {
        Assert.False(CategoriasAuditoria.EsValida("TODO"));
        Assert.False(CategoriasAuditoria.EsValida(null));
        Assert.True(CategoriasAuditoria.EsValida(CategoriasAuditoria.Operacion));
        var e = Assert.Throws<ReglaNegocioException>(() => AuditoriaConsultaServicio.Validar(new(2026, 11, 1), new(2026, 11, 2), null, "TODO", null));
        Assert.Equal("CATEGORIA_INVALIDA", e.Codigo);
    }

    [Fact]
    public void El_periodo_debe_ser_valido_y_no_pasar_de_un_ano()
    {
        Assert.Equal("PERIODO_INVALIDO", Assert.Throws<ReglaNegocioException>(() => AuditoriaConsultaServicio.Validar(new(2026, 11, 5), new(2026, 11, 4), null, null, null)).Codigo);
        Assert.Equal("PERIODO_MUY_LARGO", Assert.Throws<ReglaNegocioException>(() => AuditoriaConsultaServicio.Validar(new(2025, 1, 1), new(2026, 11, 4), null, null, null)).Codigo);
        var f = AuditoriaConsultaServicio.Validar(new(2026, 11, 1), new(2026, 11, 7), 3, "", "  Módulo\n2  ");
        Assert.Null(f.Categoria);                  // vacío = sin filtro
        Assert.Equal("Módulo2", f.Buscar);         // los saltos de línea no pasan (inyección en el registro o el CSV)
    }

    [Theory]
    [InlineData("""{"nombre":"Módulo 2","aforo":1}""", "Módulo 2")]
    [InlineData("""{"Nombres":"Luis","Apellidos":"Ramos","correo":"l@creo.pe"}""", "Luis Ramos")]
    [InlineData("""{"codigo":"N-09"}""", "N-09")]
    [InlineData("""{"correo":"a@b.pe"}""", "a@b.pe")]
    [InlineData("""{"aforo":1}""", "7")]      // sin nombre: el id
    [InlineData("no es json", "7")]
    public void El_objeto_se_toma_de_lo_que_quedo_guardado(string json, string esperado)
    {
        Assert.Equal(esperado, AuditoriaConsultaServicio.ObjetoDe("7", json));
    }

    [Fact]
    public void El_objeto_usa_el_antes_si_no_hay_despues()
    {
        Assert.Equal("Zona vieja", AuditoriaConsultaServicio.ObjetoDe("7", null, """{"nombre":"Zona vieja"}"""));
    }

    // ---------- CSV

    [Fact]
    public void El_CSV_lleva_horas_de_Lima_BOM_y_campos_a_prueba_de_formulas()
    {
        var zona = new ZonaHoraria("America/Lima");
        var bytes = CsvAuditoria.Generar([new FilaExportada(new DateTime(2026, 11, 16, 15, 30, 0, DateTimeKind.Utc), "Ana, Torres", "Editó", CategoriasAuditoria.Configuracion, "Zona", "=1+1", null)], zona);

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var texto = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var lineas = texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Fecha y hora,Usuario,Acción,Categoría,Entidad,Objeto,IP", lineas[0]);
        Assert.Equal("2026-11-16 10:30:00,\"Ana, Torres\",Editó,Configuración,Zona,'=1+1,", lineas[1]);   // 15:30 UTC = 10:30 Lima
    }

    // ---------- Sello de integridad

    private static readonly byte[] Clave = Enumerable.Range(1, 48).Select(i => (byte)i).ToArray();

    private static DatosSello Fila(long id = 1, string? despues = """{"aforo":1}""") =>
        new(id, new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc), 2, (int)AccionAuditoria.Editar, "Zona", "7", """{"aforo":2}""", despues, "10.0.0.5", "trace-1");

    [Fact]
    public void El_sello_es_determinista_y_de_32_bytes()
    {
        var a = SelladoAuditoria.Calcular(Clave, SelladoAuditoria.Origen, Fila());
        var b = SelladoAuditoria.Calcular(Clave, SelladoAuditoria.Origen, Fila());

        Assert.Equal(SelladoAuditoria.Tamano, a.Length);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Cambiar_cualquier_dato_cambia_el_sello()
    {
        var original = SelladoAuditoria.Calcular(Clave, SelladoAuditoria.Origen, Fila());
        var cambios = new[]
        {
            Fila(despues: """{"aforo":9}"""), Fila(id: 2), Fila() with { UsuarioRegistroId = 3 }, Fila() with { Accion = 5 },
            Fila() with { Entidad = "Nodo" }, Fila() with { EntidadId = "8" }, Fila() with { ValorAntes = null },
            Fila() with { Ip = "10.0.0.6" }, Fila() with { TraceId = null }, Fila() with { FechaRegistro = new DateTime(2026, 11, 16, 15, 0, 1, DateTimeKind.Utc) },
        };

        foreach (var c in cambios) Assert.NotEqual(original, SelladoAuditoria.Calcular(Clave, SelladoAuditoria.Origen, c));
    }

    [Fact]
    public void Los_campos_no_se_pueden_mover_de_uno_a_otro_ni_confundir_nulo_con_vacio()
    {
        var a = SelladoAuditoria.Calcular(Clave, SelladoAuditoria.Origen, Fila() with { ValorAntes = "ab", ValorDespues = "c" });
        var b = SelladoAuditoria.Calcular(Clave, SelladoAuditoria.Origen, Fila() with { ValorAntes = "a", ValorDespues = "bc" });
        var nulo = SelladoAuditoria.Calcular(Clave, SelladoAuditoria.Origen, Fila() with { Ip = null });
        var vacio = SelladoAuditoria.Calcular(Clave, SelladoAuditoria.Origen, Fila() with { Ip = "" });

        Assert.NotEqual(a, b);
        Assert.NotEqual(nulo, vacio);
    }

    [Fact]
    public void Cada_sello_depende_del_anterior_y_de_la_clave()
    {
        var primero = SelladoAuditoria.Calcular(Clave, SelladoAuditoria.Origen, Fila(1));
        var segundoBien = SelladoAuditoria.Calcular(Clave, primero, Fila(2));
        var segundoSinElPrimero = SelladoAuditoria.Calcular(Clave, SelladoAuditoria.Origen, Fila(2));   // se borró la fila 1
        var otraClave = SelladoAuditoria.Calcular([.. Clave.Select(b => (byte)(b ^ 1))], SelladoAuditoria.Origen, Fila(1));

        Assert.NotEqual(segundoBien, segundoSinElPrimero);
        Assert.NotEqual(primero, otraClave);   // sin la clave no se puede fabricar un sello válido
        Assert.True(SelladoAuditoria.Coincide(primero, primero.ToArray()));
        Assert.False(SelladoAuditoria.Coincide(primero, otraClave));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("no es base64!", false)]
    [InlineData("AAAA", false)]   // 3 bytes: muy corta
    public void Una_clave_mal_escrita_o_corta_no_sirve(string? clave, bool valida)
    {
        Assert.Equal(valida, SelladoAuditoria.LeerClave(clave) is not null);
    }

    [Fact]
    public void Una_clave_base64_de_32_bytes_o_mas_sirve()
    {
        Assert.NotNull(SelladoAuditoria.LeerClave(Convert.ToBase64String(Clave)));
        Assert.NotNull(SelladoAuditoria.LeerClave("  " + Convert.ToBase64String(new byte[32]) + "  "));
    }
}
