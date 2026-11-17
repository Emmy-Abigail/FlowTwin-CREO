using FlowTwin.SharedKernel;

namespace FlowTwin.Monitoreo.Domain;

/// <summary>Catálogo cat.TipoAlerta. Las alertas se calculan del estado actual del piso (ADR-25, CalculoAlertas).</summary>
public enum TipoAlerta
{
    [Catalogo("AFORO_ALTO", "Aforo alto")] AforoAlto = 1,
    [Catalogo("NODO_SIN_SENAL", "Nodo sin señal")] NodoSinSenal = 2,
    [Catalogo("TARJETA_SIN_DEVOLVER", "Tarjeta sin devolver")] TarjetaSinDevolver = 3,
    [Catalogo("ESPERA_LARGA", "Espera larga")] EsperaLarga = 4,
}
