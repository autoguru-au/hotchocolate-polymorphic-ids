using System;
using HotChocolate.Execution.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class PolymorphicIdsRequestExecutorBuilderExtensions
    {
        public static IRequestExecutorBuilder AddPolymorphicIds(
                this IRequestExecutorBuilder builder,
                PolymorphicIdsOptions? options = null)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder
                .ConfigureSchema(s => s.AddPolymorphicIds(options));
        }
    }
}
