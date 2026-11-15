using FlowTwin.Simulador;

// Uso:  dotnet run --project tools/FlowTwin.Simulador [-- --escenario escenarios/demo.json] [--api http://localhost:5090]
// Antes, la API con su propia base:  dotnet run --project src/FlowTwin.Api --launch-profile simulador
var escenarioRuta = Opcion("--escenario") ?? "escenarios/piloto.json";
if (!Path.IsPathRooted(escenarioRuta) && !File.Exists(escenarioRuta))
    escenarioRuta = Path.Combine(AppContext.BaseDirectory, escenarioRuta);

var escenario = Escenario.Leer(escenarioRuta);
if (Opcion("--api") is { } api) escenario = escenario with { Api = api };

// Lo que escribe el simulador parece real: nunca en la base de trabajo, salvo que se pida a propósito
if (escenario.ApuntaALaApiDeTrabajo && !args.Contains("--en-base-de-trabajo"))
{
    Console.Error.WriteLine(
        $"El simulador apunta a {escenario.Api}, la API de la base de trabajo: sus pacientes se mezclarían con los reales.\n" +
        "Levanta la API del simulador (dotnet run --project src/FlowTwin.Api --launch-profile simulador) y usa el puerto 5090.\n" +
        "Si de verdad quieres escribir en la base de trabajo, agrega --en-base-de-trabajo.");
    return 2;
}

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
