using System;
using System.Collections.Generic;
using System.Text;

namespace Savvy.Shared.Infrastructure.Extensions;

public interface IMigrated
{
    Task MigrateAsync(CancellationToken cancellationToken);
}