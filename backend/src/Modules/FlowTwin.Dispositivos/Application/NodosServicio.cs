using FlowTwin.Dispositivos.Domain;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Dispositivos.Application;

public sealed record NodoDto(
    int Id, string Codigo, string Mac, int ZonaId, string Zona, int PisoId, decimal? PosX, decimal? PosY,
    string Estado, string EstadoNombre, DateTime? UltimoLatidoEn, string? VersionFirmware, short AjusteRssiDb);

/// <summary>Respuesta de registrar o regenerar: la única vez que la API devuelve la clave.</summary>
public sealed record NodoConClaveDto(NodoDto Nodo, string Clave);

public sealed record RegistrarNodoRequest(string Codigo, string Mac, int ZonaId, decimal? PosX, decimal? PosY);

public sealed record EditarNodoRequest(int ZonaId, decimal? PosX, decimal? PosY, short AjusteRssiDb);

/// <summary>Registro y administración de los nodos ESP32 (F-15). La ingesta de latidos y lecturas va aparte.</summary>
public sealed class NodosServicio(IDispositivosDb db, IZonasConsultas zonas, IUsuarioActual usuario, IAuditoria auditoria)
{
    /// <summary>GET /nodos: Admin ve todos; Operador, los de sus pisos.</summary>
    public async Task<List<NodoDto>> ListarAsync(CancellationToken ct)
    {
        var lista = await db.Nodos.OrderBy(n => n.Codigo).ToListAsync(ct);
        var zonasPorId = (await zonas.ZonasAsync([.. lista.Select(n => n.ZonaId).Distinct()], ct)).ToDictionary(z => z.Id);
        return [.. lista
            .Where(n => usuario.PuedeVerPiso(zonasPorId[n.ZonaId].PisoId))
            .Select(n => Dto(n, zonasPorId[n.ZonaId]))];
    }

    /// <summary>POST /nodos (F-15): registra el nodo Sin señal y devuelve su clave una sola vez.</summary>
    public Task<NodoConClaveDto> RegistrarAsync(RegistrarNodoRequest req, CancellationToken ct) =>
        db.EnTransaccionAsync(async ct =>
        {
            var zona = await ZonaExistenteAsync(req.ZonaId, ct);
            var nodo = new Nodo
            {
                Codigo = Nodo.NormalizarCodigo(req.Codigo),
                Mac = Nodo.NormalizarMac(req.Mac) ?? req.Mac,
                ZonaId = zona.Id, PosX = req.PosX, PosY = req.PosY,
            };
            nodo.Validar();
            if (await db.Nodos.AnyAsync(n => n.Codigo == nodo.Codigo, ct))
                throw new ReglaNegocioException("NODO_CODIGO_DUPLICADO", $"Ya existe un nodo {nodo.Codigo}.");
            if (await db.Nodos.AnyAsync(n => n.Mac == nodo.Mac, ct))
                throw new ReglaNegocioException("NODO_MAC_DUPLICADA", $"Ya hay un nodo registrado con la MAC {nodo.Mac}.");

            var clave = nodo.AsignarClaveNueva();
            db.Nodos.Add(nodo);
            await db.GuardarAsync(ct);
            db.EventosNodo.Add(new EventoNodo { NodoId = nodo.Id, EstadoNodoId = nodo.EstadoNodoId, Detalle = "Registrado" });
            // La clave NUNCA va a la auditoría ni a los logs
            auditoria.Registrar(AccionAuditoria.Crear, "Nodo", nodo.Codigo, despues: Resumen(nodo, zona));
            await db.GuardarAsync(ct);
            return new NodoConClaveDto(Dto(nodo, zona), clave);
        }, ct);

    /// <summary>PUT /nodos/{id}: zona, posición y ajuste de señal (calibración en sitio, ADR-20).</summary>
    public async Task<NodoDto> EditarAsync(int id, EditarNodoRequest req, CancellationToken ct)
    {
        var nodo = await NodoVigenteAsync(id, ct);
        var antes = Resumen(nodo, await ZonaExistenteAsync(nodo.ZonaId, ct));
        var zona = await ZonaExistenteAsync(req.ZonaId, ct);
        (nodo.ZonaId, nodo.PosX, nodo.PosY, nodo.AjusteRssiDb) = (zona.Id, req.PosX, req.PosY, req.AjusteRssiDb);
        nodo.Validar();
        auditoria.Registrar(AccionAuditoria.Editar, "Nodo", nodo.Codigo, antes, Resumen(nodo, zona));
        await db.GuardarAsync(ct);
        return Dto(nodo, zona);
    }

    /// <summary>POST /nodos/{id}/regenerar-clave: la clave anterior deja de servir en ese instante.</summary>
    public async Task<NodoConClaveDto> RegenerarClaveAsync(int id, CancellationToken ct)
    {
        var nodo = await NodoVigenteAsync(id, ct);
        var clave = nodo.AsignarClaveNueva();
        auditoria.Registrar(AccionAuditoria.RegenerarClave, "Nodo", nodo.Codigo);
        await db.GuardarAsync(ct);
        return new NodoConClaveDto(Dto(nodo, await ZonaExistenteAsync(nodo.ZonaId, ct)), clave);
    }

    /// <summary>POST /nodos/{id}/baja: retira el nodo e invalida su clave.</summary>
    public async Task<NodoDto> DarDeBajaAsync(int id, CancellationToken ct)
    {
        var nodo = await db.Nodos.SingleOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new NoEncontradoException("El nodo no existe.");
        var estadoAntes = nodo.EstadoNodoId;
        db.EventosNodo.Add(nodo.DarDeBaja("Dado de baja por el Administrador"));
        auditoria.Registrar(AccionAuditoria.DarBaja, "Nodo", nodo.Codigo,
            new { estado = estadoAntes.Codigo() }, new { estado = nodo.EstadoNodoId.Codigo() });
        await db.GuardarAsync(ct);
        return Dto(nodo, await ZonaExistenteAsync(nodo.ZonaId, ct));
    }

    private async Task<Nodo> NodoVigenteAsync(int id, CancellationToken ct)
    {
        var nodo = await db.Nodos.SingleOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new NoEncontradoException("El nodo no existe.");
        if (nodo.EstadoNodoId == EstadoNodo.Baja)
            throw new ConflictoException($"El nodo {nodo.Codigo} está de baja.");
        return nodo;
    }

    private async Task<ZonaInfo> ZonaExistenteAsync(int zonaId, CancellationToken ct) =>
        await zonas.ZonaAsync(zonaId, ct)
        ?? throw new ReglaNegocioException("RN19_ZONA_INEXISTENTE", "La zona elegida no existe.");

    private static object Resumen(Nodo n, ZonaInfo zona) =>
        new { n.Codigo, n.Mac, zona = zona.Nombre, n.PosX, n.PosY, n.AjusteRssiDb };

    private static NodoDto Dto(Nodo n, ZonaInfo zona) => new(
        n.Id, n.Codigo, n.Mac, zona.Id, zona.Nombre, zona.PisoId, n.PosX, n.PosY,
        n.EstadoNodoId.Codigo(), n.EstadoNodoId.Nombre(), n.UltimoLatidoEn, n.VersionFirmware, n.AjusteRssiDb);
}
