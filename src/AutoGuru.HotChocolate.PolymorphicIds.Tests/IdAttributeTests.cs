using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Execution.Options;
using HotChocolate.Types;
using HotChocolate.Types.Relay;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace AutoGuru.HotChocolate.PolymorphicIds.Tests
{
    [UsesVerify]
    [SuppressMessage("Style", "IDE1006:Naming Styles")]
    public class IdAttributeTests
    {
        private static readonly Action<RequestExecutorOptions> _executorOptions = o =>
        {
            o.ExecutionTimeout = TimeSpan.FromMinutes(1);
            o.IncludeExceptionDetails = true;
        };

        private const string _argumentsQuery = @"
            query foo (
                $intId: ID!
                $longId: ID!
                $stringId: ID!
                $guidId: ID!
                $null: ID = null)
            {
                intId(id: $intId)
                nullableIntId(id: $intId)
                nullableIntIdGivenNull: nullableIntId(id: $null)
                intIdList(id: [$intId])
                nullableIntIdList(id: [$intId, $null])

                longId(id: $longId)
                nullableLongId(id: $longId)
                nullableLongIdGivenNull: nullableLongId(id: $null)
                longIdList(id: [$longId])
                nullableLongIdList(id: [$longId, $null])

                stringId(id: $stringId)
                nullableStringId(id: $stringId)
                nullableStringIdGivenNull: nullableStringId(id: $null)
                stringIdList(id: [$stringId])
                nullableStringIdList(id: [$stringId, $null])

                guidId(id: $guidId)
                nullableGuidId(id: $guidId)
                nullableGuidIdGivenNull: nullableGuidId(id: $null)
                guidIdList(id: [$guidId $guidId])
                nullableGuidIdList(id: [$guidId $null $guidId])
            }";

        [Fact]
        public async Task PolyId_SchemaBuildError_If_GlobalIdentification_Isnt_Enabled()
        {
            // arrange

            // act
            var schemaBuilder = SchemaBuilder.New()
                .AddQueryType<Query>()
                .AddPolymorphicIds();

            // assert
            var ex = Should.Throw<SchemaException>(() => schemaBuilder.Create());
            await Verifier.Verify(ex.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PolyId_On_Arguments(bool isEnabled)
        {
            // arrange
            var intId = 1;
            var longId = long.MaxValue;
            var stringId = "abc";
            var guidId = new Guid("26a2dc8f-4dab-408c-88c6-523a0a89a2b5");
            var services = new ServiceCollection();
            var builder = services
                .AddGraphQL()
                .AddQueryType<Query>()
                .AddType<FooPayload>()
                .AddType<Bar>()
                .AddGlobalObjectIdentification(registerNodeInterface: false)
                .AddPolymorphicIds(isEnabled
                    ? default
                    : new PolymorphicIdsOptions
                    {
                        HandleGuidIds = false,
                        HandleIntIds = false,
                        HandleLongIds = false,
                        HandleStringIds = false,
                    })
                .ModifyRequestOptions(o => o.ExecutionTimeout = TimeSpan.FromMinutes(1));
            var executor = await builder
                .BuildRequestExecutorAsync();

            // act
            var result = await executor
                .ExecuteAsync(
                    OperationRequestBuilder.New()
                        .SetDocument(_argumentsQuery)
                        .SetVariableValues(new Dictionary<string, object?>
                        {
                            ["intId"] = intId,
                            ["longId"] = longId.ToString(),
                            ["stringId"] = stringId,
                            ["guidId"] = guidId.ToString(),
                        })
                        .Build());

            // assert
            var verifySettings = new VerifySettings();
            verifySettings.UseParameters(isEnabled);

            await Verifier.Verify(result.ToJson(), verifySettings);
        }

        [Fact]
        public async Task PolyId_On_Arguments_Invalid_Id()
        {
            // arrange

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .AddPolymorphicIds()
                    .Create()
                    .MakeExecutable(_executorOptions)
                    .ExecuteAsync(
                        OperationRequestBuilder.New()
                            .SetDocument(
                                @"query foo {
                                    intId(id: ""SomethingInvalid"")
                                }")
                            .Build());

            // assert
            await Verifier.Verify(result.ToJson());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PolyId_On_Objects(bool isEnabled)
        {
            // arrange
            var someId = "1";
            var someIntId = 1;

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .AddPolymorphicIds(isEnabled
                        ? default
                        : new PolymorphicIdsOptions
                        {
                            HandleGuidIds = false,
                            HandleIntIds = false,
                            HandleLongIds = false,
                            HandleStringIds = false,
                        })
                    .Create()
                    .MakeExecutable(_executorOptions)
                    .ExecuteAsync(
                        OperationRequestBuilder.New()
                            .SetDocument(
                                @"query foo ($someId: ID! $someIntId: ID!) {
                                    foo(input: {
                                        someId: $someId
                                        someIds: [$someIntId]
                                        someNullableId: $someId
                                        someNullableIds: [$someIntId, null]
                                    })
                                    {
                                        someId
                                        someNullableId
                                        ... on FooPayload {
                                            someIds
                                            someNullableIds
                                            raw
                                        }
                                    }
                                }")
                            .SetVariableValues(new Dictionary<string, object?>
                            {
                                ["someId"] = someId,
                                ["someIntId"] = someIntId,
                            })
                            .Build());

            // assert
            var verifySettings = new VerifySettings();
            verifySettings.UseParameters(isEnabled);

            await Verifier.Verify(result.ToJson(), verifySettings);
        }

        [Fact]
        public async Task PolyId_On_Objects_Invalid_Id()
        {
            // arrange
            var someId = "1";

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .AddPolymorphicIds()
                    .Create()
                    .MakeExecutable(_executorOptions)
                    .ExecuteAsync(
                        OperationRequestBuilder.New()
                            .SetDocument(
                                @"query foo ($someId: ID!) {
                                    foo(input: {
                                        someId: $someId
                                        someIds: [""SomethingInvalid""] })
                                    {
                                        someId
                                        someNullableId
                                        ... on FooPayload {
                                            someIds
                                            someNullableIds
                                            raw
                                        }
                                    }
                                }")
                            .SetVariableValues(new Dictionary<string, object?>
                            {
                                ["someId"] = someId,
                            })
                            .Build());

            // assert
            await Verifier.Verify(result.ToJson());
        }

        // Fluent-style `.ID()` (rather than the [ID] attribute) is now supported - see issue #5.
        // LolInputType configures `someIdCustomName` as an ID via descriptor.Field(...).ID("Some"),
        // so a raw database id ("1") should be accepted polymorphically just like an attribute ID.
        [Fact]
        public async Task PolyId_On_Objects_FluentStyle()
        {
            // arrange
            var someId = "1";

            // act
            var schema =
                SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddType<LolInputType>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .AddPolymorphicIds()
                    .Create();

            var result = await schema
                    .MakeExecutable(_executorOptions)
                    .ExecuteAsync(
                        OperationRequestBuilder.New()
                            .SetDocument(
                                @"query lol ($someId: ID!) {
                                    lol(input: {
                                        someIdCustomName: $someId })
                                }")
                            .SetVariableValues(new Dictionary<string, object?>
                            {
                                ["someId"] = someId,
                            })
                            .Build());

            // assert
            await Verifier.Verify(
                new
                {
                    schema = schema.ToString(),
                    result = result.ToJson()
                });
        }

        // When a field declares an explicit type name (e.g. [ID("Thing")]), an incoming *global
        // id* for a different type is rejected, matching Hot Chocolate's own type-name
        // validation.
        [Fact]
        public async Task PolyId_Explicit_TypeName_Rejects_GlobalId_Of_Wrong_Type()
        {
            // arrange
            var wrongTypeId = new DefaultNodeIdSerializer().Format("NotAThing", 1);

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<TypeNameQuery>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .AddPolymorphicIds()
                    .Create()
                    .MakeExecutable(_executorOptions)
                    .ExecuteAsync(
                        OperationRequestBuilder.New()
                            .SetDocument(@"query ($id: ID!) { thing(id: $id) }")
                            .SetVariableValues(new Dictionary<string, object?>
                            {
                                ["id"] = wrongTypeId,
                            })
                            .Build());

            // assert
            await Verifier.Verify(result.ToJson());
        }

        // Raw database ids (no type name) and global ids of the matching type are both accepted.
        [Fact]
        public async Task PolyId_Explicit_TypeName_Accepts_RawId_And_MatchingType()
        {
            // arrange
            var matchingTypeId = new DefaultNodeIdSerializer().Format("Thing", 1);

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<TypeNameQuery>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .AddPolymorphicIds()
                    .Create()
                    .MakeExecutable(_executorOptions)
                    .ExecuteAsync(
                        OperationRequestBuilder.New()
                            .SetDocument(
                                @"query ($raw: ID! $matching: ID!) {
                                    byRawId: thing(id: $raw)
                                    byMatchingTypeName: thing(id: $matching)
                                }")
                            .SetVariableValues(new Dictionary<string, object?>
                            {
                                ["raw"] = "1",
                                ["matching"] = matchingTypeId,
                            })
                            .Build());

            // assert: both resolve to "1", no errors.
            await Verifier.Verify(result.ToJson());
        }

        #region Standard ID still works

        // NOTE: This exercises Hot Chocolate's built-in formatter (no AddPolymorphicIds).
        // As of HC14 there is an upstream bug where a nullable list of a value-type ID
        // (e.g. int?[], long?[], Guid?[]) silently converts null elements to the type
        // default (0 / Guid.Empty) instead of null - see
        // https://github.com/ChilliCream/graphql-platform/issues/9811. The verified
        // snapshot therefore shows e.g. "1, 0" for nullableIntIdList. Revisit (and restore
        // null) if/when that bug is fixed upstream. Our own formatter preserves nulls
        // correctly - see PolyId_On_Arguments.
        [Fact]
        public async Task Id_On_Arguments()
        {
            // arrange
            var idSerializer = new DefaultNodeIdSerializer();
            var intId = idSerializer.Format("Some", 1);
            var longId = idSerializer.Format("Some", long.MaxValue);
            var stringId = idSerializer.Format("Some", "abc");
            var guidId = idSerializer.Format("Some", new Guid("26a2dc8f-4dab-408c-88c6-523a0a89a2b5"));

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddType<Bar>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .Create()
                    .MakeExecutable()
                    .ExecuteAsync(
                        OperationRequestBuilder.New()
                            .SetDocument(_argumentsQuery)
                            .SetVariableValues(new Dictionary<string, object?>
                            {
                                ["intId"] = intId,
                                ["longId"] = longId,
                                ["stringId"] = stringId,
                                ["guidId"] = guidId,
                            })
                            .Build());

            // assert
            await Verifier.Verify(result.ToJson());
        }

        [Fact]
        public async Task Id_On_Objects()
        {
            // arrange
            var idSerializer = new DefaultNodeIdSerializer();
            var someId = idSerializer.Format("Some", "1");
            var someIntId = idSerializer.Format("Some", 1);

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .Create()
                    .MakeExecutable()
                    .ExecuteAsync(
                        OperationRequestBuilder.New()
                            .SetDocument(
                                @"query foo ($someId: ID! $someIntId: ID!) {
                                    foo(input: {
                                        someId: $someId
                                        someIds: [$someIntId]
                                        someNullableId: $someId
                                        someNullableIds: [$someIntId]
                                    })
                                    {
                                        someId
                                        someNullableId
                                        ... on FooPayload {
                                            someIds
                                            someNullableIds
                                        }
                                    }
                                }")
                            .SetVariableValues(new Dictionary<string, object?>
                            {
                                ["someId"] = someId,
                                ["someIntId"] = someIntId,
                            })
                            .Build());

            // assert
            await Verifier.Verify(new
            {
                result = result.ToJson(),
                someId,
                someIntId
            });
        }

        // NOTE: This exercises Hot Chocolate's built-in formatter (no AddPolymorphicIds).
        // someNullableIds is a nullable list of a value-type ID, so it is affected by the
        // upstream HC14 bug where null list elements become the type default instead of
        // null - see https://github.com/ChilliCream/graphql-platform/issues/9811. The
        // verified snapshot reflects that (the null becomes an encoded "0"). Revisit if/when
        // it's fixed upstream. Our own formatter preserves nulls - see PolyId_On_Objects.
        [Fact]
        public async Task Id_On_Objects_Given_Nulls()
        {
            // arrange
            var idSerializer = new DefaultNodeIdSerializer();
            var someId = idSerializer.Format("Some", "1");
            var someIntId = idSerializer.Format("Some", 1);

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .Create()
                    .MakeExecutable()
                    .ExecuteAsync(
                        OperationRequestBuilder.New()
                            .SetDocument(
                                @"query foo ($someId: ID! $someIntId: ID!) {
                                    foo(input: {
                                        someId: $someId
                                        someIds: [$someIntId]
                                        someNullableId: null
                                        someNullableIds: [$someIntId, null]
                                    })
                                    {
                                        someId
                                        someNullableId
                                        ... on FooPayload {
                                            someIds
                                            someNullableIds
                                        }
                                    }
                                }")
                            .SetVariableValues(new Dictionary<string, object?>
                            {
                                ["someId"] = someId,
                                ["someIntId"] = someIntId,
                            })
                            .Build());

            // assert
            await Verifier.Verify(new
            {
                result = result.ToJson(),
                someId,
                someIntId
            });
        }

        [Fact]
        public async Task Id_On_Objects_InvalidType()
        {
            // arrange
            var idSerializer = new DefaultNodeIdSerializer();
            var someId = idSerializer.Format("Some", Guid.Empty);

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .Create()
                    .MakeExecutable()
                    .ExecuteAsync(
                        OperationRequestBuilder.New()
                            .SetDocument(
                                @"query foo ($someId: ID!) {
                                    foo(input: { someId: $someId someIds: [$someId] }) {
                                        someId
                                        ... on FooPayload {
                                            someIds
                                        }
                                    }
                                }")
                            .SetVariableValues(new Dictionary<string, object?>
                            {
                                ["someId"] = someId,
                            })
                            .Build());

            // assert
            await Verifier.Verify(new
            {
                result = result.ToJson(),
                someId
            });
        }

        [Fact]
        public async Task Id_On_Objects_InvalidId()
        {
            // arrange
            var someId = "abc";

            // act
            var schema = SchemaBuilder.New()
                .AddQueryType<Query>()
                .AddType<FooPayload>()
                .AddGlobalObjectIdentification(registerNodeInterface: false)
                .Create();

            var executableSchema = schema
                .MakeExecutable();

            var result = await executableSchema
                .ExecuteAsync(
                    OperationRequestBuilder.New()
                        .SetDocument(
                            @"query foo ($someId: ID!) {
                                foo(input: { someId: $someId someIds: [$someId] }) {
                                    someId
                                    ... on FooPayload {
                                        someIds
                                    }
                                }
                            }")
                        .SetVariableValues(new Dictionary<string, object?>
                        {
                            ["someId"] = someId,
                        })
                        .Build());

            // assert
            await Verifier.Verify(new
            {
                result = result.ToJson(),
                someId
            });
        }

        #endregion

        [Fact]
        public async Task Schema()
        {
            // arrange / act
            var schema =
                SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .AddPolymorphicIds()
                    .Create()
                    .ToString();

            // assert
            await Verifier.Verify(schema);
        }

        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Can't be static for HC")]
        public class Query
        {
            public string IntId([ID] int id) => id.ToString();
            public string IntIdList([ID] int[] id) =>
                string.Join(", ", id.Select(t => t.ToString()));

            public string NullableIntId([ID] int? id) => id?.ToString() ?? "null";
            public string NullableIntIdList([ID] int?[] id) =>
                string.Join(", ", id.Select(t => t?.ToString() ?? "null"));

            public string LongId([ID] long id) => id.ToString();
            public string LongIdList([ID] long[] id) =>
                string.Join(", ", id.Select(t => t.ToString()));

            public string NullableLongId([ID] long? id) => id?.ToString() ?? "null";
            public string NullableLongIdList([ID] long?[] id) =>
                string.Join(", ", id.Select(t => t?.ToString() ?? "null"));

            public string StringId([ID] string id) => id;
            public string StringIdList([ID] string[] id) =>
                string.Join(", ", id.Select(t => t.ToString()));

            public string NullableStringId([ID] string? id) => id ?? "null";
            public string NullableStringIdList([ID] string?[] id) =>
                string.Join(", ", id.Select(t => t?.ToString() ?? "null"));

            public string GuidId([ID] Guid id) => id.ToString();
            public string GuidIdList([ID] IReadOnlyList<Guid> id) =>
                string.Join(", ", id.Select(t => t.ToString()));

            public string NullableGuidId([ID] Guid? id) => id?.ToString() ?? "null";
            public string NullableGuidIdList([ID] IReadOnlyList<Guid?> id) =>
                string.Join(", ", id.Select(t => t?.ToString() ?? "null"));

            public IFooPayload Foo(FooInput input) =>
                new FooPayload(input.SomeId, input.SomeNullableId, input.SomeIds, input.SomeNullableIds);

            public int Lol(LolInput input) => input.SomeId;
        }

        public class FooInput
        {
            public FooInput(
                string someId,
                string? someNullableId,
                IReadOnlyList<int> someIds,
                IReadOnlyList<int?>? someNullableIds)
            {
                SomeId = someId;
                SomeNullableId = someNullableId;
                SomeIds = someIds;
                SomeNullableIds = someNullableIds;
            }

            [ID("Some")] public string SomeId { get; }

            [ID("Some")] public string? SomeNullableId { get; }

            [ID("Some")] public IReadOnlyList<int> SomeIds { get; }

            [ID("Some")] public IReadOnlyList<int?>? SomeNullableIds { get; }
        }

        public class FooPayload : IFooPayload
        {
            public FooPayload(
                string someId,
                string? someNullableId,
                IReadOnlyList<int> someIds,
                IReadOnlyList<int?>? someNullableIds)
            {
                SomeId = someId;
                SomeNullableId = someNullableId;
                SomeIds = someIds;
                SomeNullableIds = someNullableIds;
            }

            [ID("Bar")] public string SomeId { get; }

            [ID("Bar")] public IReadOnlyList<int> SomeIds { get; }

            [ID("Bar")] public string? SomeNullableId { get; }

            [ID("Bar")] public IReadOnlyList<int?>? SomeNullableIds { get; }

            public string Raw =>
                $"{nameof(SomeId)}: {SomeId}, " +
                $"{nameof(SomeIds)}: [{string.Join(", ", SomeIds)}], " +
                $"{nameof(SomeNullableId)}: {SomeNullableId ?? "null"}, " +
                $"{nameof(SomeNullableIds)}: {(SomeNullableIds == null ? "null" : "[" + string.Join(", ", SomeNullableIds.Select(x => x?.ToString() ?? "null").ToArray()) + "]")}";
        }

        public interface IFooPayload
        {
            [ID] string SomeId { get; }

            [ID] public string? SomeNullableId { get; }

            [ID] IReadOnlyList<int> SomeIds { get; }

            [ID] IReadOnlyList<int?>? SomeNullableIds { get; }

            string Raw { get; }
        }

        [Node]
        public class Bar
        {
            public int Id { get; }

            public Bar(int id)
            {
                Id = id;
            }

            public static Bar GetBarAsync(int id) => new(id);
        }

        public class LolInput
        {
            public int SomeId { get; set; }
        }

        public class LolInputType : InputObjectType<LolInput>
        {
            protected override void Configure(IInputObjectTypeDescriptor<LolInput> descriptor)
            {
                descriptor.Field(x => x.SomeId).Name("someIdCustomName").ID("Some");
            }
        }

        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Can't be static for HC")]
        public class TypeNameQuery
        {
            public string Thing([ID("Thing")] int id) => id.ToString();
        }
    }
}
