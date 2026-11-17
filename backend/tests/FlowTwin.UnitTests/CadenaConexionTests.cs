using FlowTwin.Persistence;
using Microsoft.Data.SqlClient;

namespace FlowTwin.UnitTests;

/// <summary>BaseDatos:Nombre: otra base con el mismo secreto (perfil «simulador»).</summary>
public class CadenaConexionTests
{
    private const string Cadena = "Server=localhost,1433;Database=FlowTwin;User Id=sa;Password=x;TrustServerCertificate=True";

    [Fact]
    public void Sin_nombre_la_cadena_queda_igual()
    {
        Assert.Equal(Cadena, CadenaConexion.ConBase(Cadena, null));
        Assert.Equal(Cadena, CadenaConexion.ConBase(Cadena, " "));
    }

    [Fact]
    public void Con_nombre_cambia_solo_la_base()
    {
        var b = new SqlConnectionStringBuilder(CadenaConexion.ConBase(Cadena, "FlowTwin_Simulador"));

        Assert.Equal("FlowTwin_Simulador", b.InitialCatalog);
        Assert.Equal("localhost,1433", b.DataSource);
        Assert.Equal("sa", b.UserID);
        Assert.Equal("x", b.Password);
    }

    [Theory]
    [InlineData("FlowTwin;Database=master")]   // no se puede colar otra opción en la cadena
    [InlineData("Flow Twin")]
    [InlineData("1Base")]
    public void Un_nombre_raro_se_rechaza(string nombre) =>
        Assert.Throws<InvalidOperationException>(() => CadenaConexion.ConBase(Cadena, nombre));
}
