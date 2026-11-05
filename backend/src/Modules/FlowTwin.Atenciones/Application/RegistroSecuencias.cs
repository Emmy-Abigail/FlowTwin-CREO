using System.Collections.Concurrent;

namespace FlowTwin.Atenciones.Application;

/// <summary>
/// Evita guardar dos veces un lote reenviado (idempotencia). Si el ESP32 envía un lote, el servidor lo
/// guarda pero la respuesta se pierde, el nodo lo reenvía con la misma secuencia.
///
/// Recuerda las últimas <see cref="Capacidad"/> secuencias de cada nodo, NO "la mayor": si el ESP32 se
/// reinicia, su secuencia vuelve a 0, y con la regla "rechazar lo menor" se perderían todas sus lecturas
/// hasta superar el número anterior. Vive en memoria (singleton): tras reiniciar la API se olvida, y
/// lo peor que pasa es guardar una vez de más un lote reenviado justo en ese momento.
/// </summary>
public sealed class RegistroSecuencias
{
    /// <summary>Un lote cada 2 s: 64 secuencias cubren ~2 minutos de reenvíos.</summary>
    public const int Capacidad = 64;

    private readonly ConcurrentDictionary<int, Ventana> porNodo = new();

    /// <summary>true si la secuencia es nueva y quedó anotada; false si ya se había recibido.</summary>
    public bool Registrar(int nodoId, long secuencia) => porNodo.GetOrAdd(nodoId, _ => new Ventana()).Registrar(secuencia);

    /// <summary>Si guardar el lote falló, se olvida la secuencia para aceptar el reintento.</summary>
    public void Olvidar(int nodoId, long secuencia)
    {
        if (porNodo.TryGetValue(nodoId, out var ventana)) ventana.Olvidar(secuencia);
    }

    private sealed class Ventana
    {
        private readonly Lock candado = new();
        private readonly Queue<long> orden = new();
        private readonly HashSet<long> vistas = [];

        public bool Registrar(long secuencia)
        {
            lock (candado)
            {
                if (!vistas.Add(secuencia)) return false;
                orden.Enqueue(secuencia);
                if (orden.Count > Capacidad) vistas.Remove(orden.Dequeue());
                return true;
            }
        }

        public void Olvidar(long secuencia)
        {
            lock (candado) vistas.Remove(secuencia);
        }
    }
}
