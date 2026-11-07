using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Notifications.Core.Ports.Outbound;

/// <summary>Transactional outbox of the Notifications module: events commit together with the module's unit of work.</summary>
public interface INotificationsOutbox : IOutbox;
