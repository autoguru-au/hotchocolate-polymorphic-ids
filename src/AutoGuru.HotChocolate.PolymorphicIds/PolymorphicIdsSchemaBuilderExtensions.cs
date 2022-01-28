using System;
using AutoGuru.HotChocolate.PolymorphicIds;
using HotChocolate;

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

            options ??= new PolymorphicIdsOptions();
            
            return builder
                .SetContextData(typeof(PolymorphicIdsOptions).FullName!, options)
                .TryAddTypeInterceptor<PolymorphicIdsTypeInterceptor>();
        }
    }
}
