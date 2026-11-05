using FlowTwin.Simulador;

// Uso:  dotnet run --project tools/FlowTwin.Simulador [-- --escenario escenarios/demo.json] [--api http://localhost:5080]
var escenarioRuta = Opcion("--escenario") ?? "escenarios/piloto.json";
if (!Path.IsPathRooted(escenarioRuta) && !File.Exists(escenarioRuta))
    escenarioRuta = Path.Combine(AppContext.BaseDirectory, escenarioRuta);

var escenario = Escenario.Leer(escenarioRuta);
if (Opcion("--api") is { } api) escenario = escenario with { Api = api };

using var cancelar = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancelar.Cancel(); };

using var http = new HttpClient { BaseAddress = new Uri(escenario.Api), Timeout = TimeSpan.FromSeconds(5) };
var azar = escenario.Semilla is int semilla ? new Random(semilla) : new Random();

Console.WriteLine($"FlowTwin.Simulador · escenario {Path.GetFileName(escenarioRuta)} · API {escenario.Api}");
try
{
    await new Simulacion(escenario, new ClienteApi(http, escenario), azar, Console.Out).EjecutarAsync(cancelar.Token);
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"No se pudo conectar con la API en {escenario.Api}: {ex.Message}. ¿Está corriendo?");
    return 1;
}
return 0;

string? Opcion(string nombre)
{
    var i = Array.IndexOf(args, nombre);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
