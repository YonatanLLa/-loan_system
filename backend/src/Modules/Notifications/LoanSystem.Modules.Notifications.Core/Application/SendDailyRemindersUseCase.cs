using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Notifications.Core.Ports.Inbound;

namespace LoanSystem.Modules.Notifications.Core.Application;

internal sealed class SendDailyRemindersUseCase : ISendDailyRemindersUseCase
{
    public Task<Result> ExecuteAsync(CancellationToken ct = default)
    {
        // TODO: send SMS/WhatsApp/Email payment reminders
        return Task.FromResult(Result.Success());
    }
}
