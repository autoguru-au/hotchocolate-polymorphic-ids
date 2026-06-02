using System;
using AutoGuru.HotChocolate.Types.Relay;
using HotChocolate;
using HotChocolate.Features;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class PolymorphicIdsSchemaBuilderExtensions
    {
        public static ISchemaBuilder AddPolymorphicIds(
            this ISchemaBuilder builder,
            PolymorphicIdsOptions? options = null)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            // As of HC16 the string-keyed schema context data is replaced by the typed
            // feature collection, so we stash the options as a feature keyed by their type.
            builder.Features.Set(options ?? new PolymorphicIdsOptions());

            return builder
                .TryAddTypeInterceptor<PolymorphicIdsTypeInterceptor>();
        }
    }
}
