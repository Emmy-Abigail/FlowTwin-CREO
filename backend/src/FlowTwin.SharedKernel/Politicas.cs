namespace FlowTwin.SharedKernel;

/// <summary>
/// Nombres de las políticas de autorización (sección Seguridad → Autorización).
/// La Api las define; los módulos las usan en sus endpoints con RequireAuthorization(...).
/// </summary>
public static class Politicas
{
    public const string SoloAdmin = "SoloAdmin";
    public const string SoloOperador = "SoloOperador";
    public const string OperadorOAdmin = "OperadorOAdmin";
}

/// <summary>Nombres de los claims del token de acceso.</summary>
public static class Claims
{
    public const string Rol = "rol";
    public const string Piso = "piso";
    public const string DebeCambiar = "debeCambiar";
}
