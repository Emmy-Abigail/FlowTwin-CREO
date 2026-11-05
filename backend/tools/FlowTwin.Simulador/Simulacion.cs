namespace FlowTwin.Simulador;

public enum Etapa { Admision, Espera, Consulta }

/// <summary>Un paciente simulado: su tarjeta, su atención y dónde está.</summary>
public sealed class Paciente(TarjetaApi tarjeta, int numero, string atencion)
{
    public TarjetaApi Tarjeta { get; } = tarjeta;
    public int Numero { get; } = numero;
    public string Atencion { get; } = atencion;
    public Etapa Etapa { get; set; } = Etapa.Admision;
    public DateTime FinEtapa { get; set; }
    /// <summary>Un punto fijo dentro de la zona mientras dura la etapa, en metros.</summary>
    public (double X, double Y) Posicion { get; set; }
}

/// <summary>
/// Modo Piloto (sección "Simulador de nodos"): llegan pacientes, recepción les entrega una tarjeta por la
/// API, recorren Admisión → Sala de espera → consulta y devuelven la tarjeta. Mientras tanto cada nodo
/// simulado envía cada 2 s lo que "ve" según la distancia, y un latido cada 30 s.
/// </summary>
public sealed class Simulacion(Escenario escenario, ClienteApi api, Random azar, TextWriter salida)
{
    private static readonly TimeSpan CadaLote = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CadaLatido = TimeSpan.FromSeconds(30);

    private readonly List<Paciente> pacientes = [];
    private readonly Dictionary<string, EstadoNodo> nodos = [];
    private Dictionary<int, ZonaApi> zonas = [];
    private DateTime proximaLlegada;
    private bool avisoSinTarjetas;

    private sealed class EstadoNodo(NodoSimulado config, (double X, double Y) posicion)
    {
        public NodoSimulado Config { get; } = config;
        public (double X, double Y) Posicion { get; } = posicion;
        public long Secuencia { get; set; }
        public DateTime ProximoLatido { get; set; }
        public DateTime EnPausaHasta { get; set; }
    }

    public async Task EjecutarAsync(CancellationToken detener)
    {
        await PrepararAsync(detener);
        try
        {
            using var temporizador = new PeriodicTimer(CadaLote);
            do
            {
                var ahora = DateTime.UtcNow;
                await LlegadasAsync(ahora, detener);
                await AvanzarAsync(ahora, detener);
                await EnviarAsync(ahora, detener);
            } while (await temporizador.WaitForNextTickAsync(detener));
        }
        catch (OperationCanceledException) when (detener.IsCancellationRequested) { }
        finally
        {
            await DevolverTodasAsync();
        }
    }

    private async Task PrepararAsync(CancellationToken ct)
    {
        var geometria = await api.GeometriaAsync(ct);
        zonas = geometria.Zonas.ToDictionary(z => z.Id);
        foreach (var n in escenario.Nodos)
        {
            var enPlano = geometria.Nodos.SingleOrDefault(g => g.Codigo == n.Codigo)
                ?? throw new InvalidOperationException($"El nodo {n.Codigo} no está en la geometría del piso {escenario.PisoId}: regístralo con posición.");
            nodos[n.Codigo] = new EstadoNodo(n, (enPlano.X, enPlano.Y)) { ProximoLatido = DateTime.UtcNow };
        }
        // Tarjetas que quedaron por desinfectar de una corrida anterior vuelven a estar disponibles
        foreach (var t in (await api.TarjetasAsync(ct)).Where(t => t.Estado == "POR_DESINFECTAR"))
            await api.DesinfectarAsync(t.Id, ct);
        proximaLlegada = DateTime.UtcNow;
        Escribir($"Simulando {geometria.Nombre}: {nodos.Count} nodos, hasta {escenario.MaxPacientes} pacientes. Ctrl+C para terminar.");
    }

    private async Task LlegadasAsync(DateTime ahora, CancellationToken ct)
    {
        if (ahora < proximaLlegada || pacientes.Count >= escenario.MaxPacientes) return;
        var libre = (await api.TarjetasAsync(ct)).Where(t => t.Estado == "DISPONIBLE").OrderBy(_ => azar.Next()).FirstOrDefault();
        if (libre is null)
        {
            if (!avisoSinTarjetas) Escribir("No hay tarjetas disponibles: el próximo paciente espera.");
            avisoSinTarjetas = true;
            return;
        }
        avisoSinTarjetas = false;

        var entrega = await api.EscanearAsync(libre.Codigo, ct);
        var p = new Paciente(entrega.Tarjeta, int.Parse(libre.Codigo.Split('-')[1]), entrega.Atencion?.Codigo ?? "?");
        Pasar(p, Etapa.Admision, escenario.ZonaAdmision, escenario.EnAdmision, ahora);
        pacientes.Add(p);
        proximaLlegada = ahora + escenario.LlegadaCada.Elegir(azar);
        Escribir($"{p.Atencion} llega y recibe {libre.Codigo}");
    }

    private async Task AvanzarAsync(DateTime ahora, CancellationToken ct)
    {
        foreach (var p in pacientes.Where(p => ahora >= p.FinEtapa).ToList())
        {
            switch (p.Etapa)
            {
                case Etapa.Admision:
                    Pasar(p, Etapa.Espera, escenario.ZonaEspera, escenario.Espera, ahora);
                    break;
                case Etapa.Espera:
                    Pasar(p, Etapa.Consulta, escenario.ZonaConsulta, escenario.Consulta, ahora);
                    break;
                case Etapa.Consulta:
                    await DevolverAsync(p, ct);
                    pacientes.Remove(p);
                    break;
            }
        }
    }

    private void Pasar(Paciente p, Etapa etapa, int zonaId, Rango duracion, DateTime ahora)
    {
        var zona = zonas[zonaId];
        // Un punto al azar dentro de la zona, a 30 cm de las paredes
        p.Posicion = (zona.X + 0.3 + azar.NextDouble() * (zona.Ancho - 0.6), zona.Y + 0.3 + azar.NextDouble() * (zona.Fondo - 0.6));
        p.Etapa = etapa;
        p.FinEtapa = ahora + duracion.Elegir(azar);
        if (etapa != Etapa.Admision) Escribir($"{p.Atencion} pasa a {zona.Nombre} hasta las {p.FinEtapa.ToLocalTime():HH:mm:ss}");
    }

    private async Task DevolverAsync(Paciente p, CancellationToken ct)
    {
        await api.EscanearAsync(p.Tarjeta.Codigo, ct);
        await api.DesinfectarAsync(p.Tarjeta.Id, ct);
        Escribir($"{p.Atencion} devuelve {p.Tarjeta.Codigo}");
    }

    private async Task EnviarAsync(DateTime ahora, CancellationToken ct)
    {
        foreach (var nodo in nodos.Values.Where(n => ahora >= n.EnPausaHasta))
        {
            if (ahora >= nodo.ProximoLatido)
            {
                var (latido, _) = await api.LatidoAsync(nodo.Config, ct);
                if (Atender(nodo, latido, TimeSpan.FromSeconds(60), ahora)) nodo.ProximoLatido = ahora + CadaLatido;
            }

            var lecturas = pacientes
                .Select(p => (p, rssi: ModeloSenal.Rssi(ModeloSenal.Distancia(p.Posicion, nodo.Posicion), escenario.RuidoDb, azar)))
                .Where(x => x.rssi >= ModeloSenal.Sensibilidad)
                .Select(x => new LecturaApi(escenario.UuidBle(x.p.Numero), x.rssi, azar.Next(0, 1500)))
                .ToList();
            if (lecturas.Count == 0) continue;   // el contrato permite no enviar un lote vacío

            var (respuesta, espera) = await api.LoteAsync(nodo.Config, nodo.Secuencia, lecturas, ct);
            if (Atender(nodo, respuesta, espera, ahora)) nodo.Secuencia++;
        }
    }

    /// <summary>Sección 6 del contrato. Devuelve true si el envío terminó (aceptado o descartado).</summary>
    private bool Atender(EstadoNodo nodo, RespuestaNodo respuesta, TimeSpan espera, DateTime ahora)
    {
        switch (respuesta)
        {
            case RespuestaNodo.Recibido:
                return true;
            case RespuestaNodo.Rechazado:
                Escribir($"{nodo.Config.Codigo}: la API rechazó el formato (400); se descarta, revisar el simulador");
                return true;
            case RespuestaNodo.ClaveInvalida:
                Escribir($"{nodo.Config.Codigo}: clave o MAC inválidas (401); reintenta en 60 s");
                break;
            case RespuestaNodo.Esperar:
                Escribir($"{nodo.Config.Codigo}: demasiadas peticiones (429); espera {espera.TotalSeconds:0} s");
                break;
            case RespuestaNodo.SinConexion:
                Escribir($"{nodo.Config.Codigo}: sin conexión con la API");
                break;
        }
        nodo.EnPausaHasta = ahora + espera;
        return false;
    }

    /// <summary>Al terminar (Ctrl+C), la recepción recibe todas las tarjetas: la base no queda con tarjetas huérfanas.</summary>
    private async Task DevolverTodasAsync()
    {
        if (pacientes.Count == 0) return;
        using var tiempo = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Escribir($"Devolviendo {pacientes.Count} tarjetas…");
        foreach (var p in pacientes.ToList())
        {
            try { await DevolverAsync(p, tiempo.Token); }
            catch (Exception ex) { Escribir($"No se pudo devolver {p.Tarjeta.Codigo}: {ex.Message}"); }
        }
    }

    private void Escribir(string mensaje) => salida.WriteLine($"{DateTime.Now:HH:mm:ss}  {mensaje}");
}
