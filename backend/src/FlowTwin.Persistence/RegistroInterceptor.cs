using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FlowTwin.Persistence;

/// <summary>
/// Pedido del patrocinador (ADR-18), resuelto en un solo lugar: antes de cada SaveChanges
/// llena las columnas de registro y convierte cualquier "borrar" en una baja lógica (RN-28).
/// Así ningún caso de uso puede olvidarlas.
/// </summary>
public sealed class RegistroInterceptor(IUsuarioActual usuario, IReloj reloj) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Aplicar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Aplicar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Aplicar(DbContext? contexto)
    {
        if (contexto is null) return;
        var ahora = reloj.AhoraUtc;

        foreach (var entrada in contexto.ChangeTracker.Entries())
        {
            switch (entrada.State)
            {
                case EntityState.Added when entrada.Entity is IRegistroCreacion nueva:
                    nueva.FechaRegistro = ahora;
                    nueva.UsuarioRegistroId = usuario.Id;
                    break;

                case EntityState.Modified when entrada.Entity is IRegistroActualizacion cambiada:
                    cambiada.FechaActualizacion = ahora;
                    cambiada.UsuarioActualizacionId = usuario.Id;
                    // Las columnas de creación no se reescriben nunca
                    entrada.Property(nameof(IRegistroCreacion.FechaRegistro)).IsModified = false;
                    entrada.Property(nameof(IRegistroCreacion.UsuarioRegistroId)).IsModified = false;
                    break;

                case EntityState.Deleted when entrada.Entity is IEstadoRegistro baja:
                    // RN-28: "eliminar" nunca borra la fila; la marca como dada de baja.
                    entrada.State = EntityState.Modified;
                    baja.EstadoRegistro = false;
                    if (entrada.Entity is IRegistroActualizacion r)
                    {
                        r.FechaActualizacion = ahora;
                        r.UsuarioActualizacionId = usuario.Id;
                    }
                    break;

                case EntityState.Deleted:
                    // Eventos, atenciones y auditoría son historia: no se borran (ADR-06).
                    throw new InvalidOperationException(
                        $"{entrada.Entity.GetType().Name} es inmutable y no se puede eliminar.");
            }
        }
    }
}
