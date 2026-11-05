using FlowTwin.SharedKernel;

namespace FlowTwin.Monitoreo.Domain;

/// <summary>Catálogo cat.TipoAlerta. Las alertas y el hub SignalR llegan en el Avance 2.</summary>
public enum TipoAlerta
{
    [Catalogo("AFORO_ALTO", "Aforo alto")] AforoAlto = 1,
    [Catalogo("NODO_SIN_SENAL", "Nodo sin señal")] NodoSinSenal = 2,
    [Catalogo("TARJETA_SIN_DEVOLVER", "Tarjeta sin devolver")] TarjetaSinDevolver = 3,
    [Catalogo("ESPERA_LARGA", "Espera larga")] EsperaLarga = 4,
}
