# Polymorphic IDs

<div>
  <p>
	  <a href="https://www.nuget.org/packages/AutoGuru.HotChocolate.PolymorphicIds"><img alt="Nuget version" src="https://img.shields.io/nuget/v/AutoGuru.HotChocolate.PolymorphicIds"></a>
	  <a href="https://www.nuget.org/packages/AutoGuru.HotChocolate.PolymorphicIds"><img alt="NuGet downloads" src="https://img.shields.io/nuget/dt/AutoGuru.HotChocolate.PolymorphicIds"></a>	  
      <a href="https://codecov.io/gh/autoguru-au/hotchocolate-extensions/PolymorphicIds">
        <img src="https://codecov.io/gh/autoguru-au/hotchocolate-polymorphic-ids/branch/main/graph/badge.svg?token=95TCHXVJTS"/>
      </a>    
  </p>
</div>

This package adds support to [ChilliCream](https://chillicream.com/)'s 
[HotChocolate](https://github.com/ChilliCream/hotchocolate) for 
polymorphic Relay / Global IDs so that you can pass the database id in as an `ID` in 
an input/argument and it'll be accepted.

For example, if you read the description of an `ID` type in GitHub's GraphQL API, it says:
> When expected as an input type, any string (such as "4") or integer (such as 4) input value will be accepted as an ID.

The following becomes possible (on args/input fields annotated with HotChocolate's `[ID]` attribute).
```graphql
# Schema
type Query {
  booking(id: ID!): Booking
}

# Query
query {
  bookingByGlobalId: booking(id: "TheGlobalIdValue7sghdyg=") { ... }
  
  bookingByDbId: booking(id: 1) { ... }
  
  bookingByDbIdString: booking(id: "1") { ... }
}
```

## More details 

### Why would you do this?

1. To achieve friendly URLs, like `/booking/123`, you need to be able to get a booking by its database id (`123`) as the client doesn't have the global ID. But it's nasty to have to expose a `bookingByDbId(id: Int!)` field to do so.
1. For easier debugging. As humans we use database ids. So if you've got one, you can just pass it on through.

### What's supported?

Currently only arguments / input fields that are annotated with the `[ID]` attribute will be noticed and have support added for handling different ids.
Specifically, the fluent-style declaration `.ID()` won't be handled right now; though support could 
be added for it in the future so shout out if you need it.

IDs that are internally represented with `int`, `Guid`, `long` or `string`, and their nullable equivalents will be handled. 
You can opt-out of each's support as required.

For integer-based IDs, you can pass `"1"` or `1` and both will be accepted.

For all other types, you need to pass the string value, e.g. 
* `"26a2dc8f-4dab-408c-88c6-523a0a89a2b5"` for a guid-based ID
* `"123456789"` for a long-based ID

### Any downsides?

1. Strings are a problem. It's difficult to distinguish between the global id format and a string database id. 
As such, in this case, we try to read it as a global id and if that throws we consider it a database id. 
The one problem being that invalid global ids, e.g. you missed one char, will be considered a database id. 
If you don't have string db ids, it's a good idea to just turn off their handling so an invalid global id would still throw (see setup below).
2. There's a performance hit to the interception, but it'd be barely measurable.
3. Once you go down this path, it's very difficult to go back as your clients will start to rely on this.

## Setup

Install a [compatible version](#Compatibility) of the 
[package from NuGet](https://www.nuget.org/packages/AutoGuru.HotChocolate.PolymorphicIds)

```bash
dotnet add package AutoGuru.HotChocolate.PolymorphicIds
```

Configure it on your schema (`ISchemaBuilder`) or executor (`IRequestExecutorBuilder`):
```c#
.AddGlobalObjectIdentification() // Required since Hot Chocolate v12.6.0+
.AddPolymorphicIds(new PolymorphicIdsOptions
{
    HandleGuidIds = false,    // true by default
    HandleIntIds = true,      // true by default
    HandleLongIds = false,    // true by default
    HandleStringIds = false,  // true by default
});
```

### Adding a dbId field declaratively

At AutoGuru we add a `dbId` field to all our nodes. Since this is essentially the same as declaring the node's `id` field, we've got
some helpers for this that can be found [here](https://gist.github.com/benmccallum/89d4d5b604d67094418956db43386ce5).

Currently this is a manual/explicit thing you need to do, but in future this will ideally become automatic via a type interceptor
if I can figure out how to get that to work (last attempt failed :P).


## Compatibility

We depend on [HotChocolate.Execution](https://www.nuget.org/packages/HotChocolate.Execution)
which can bring breaking changes from time to time and require a major bump our end.
Compatibility is listed below.

We strive to match Hot Chocolate's supported .NET target frameworks, though this might not always be possible.

| HotChocolate | Polymorphic IDs | Our docs   |
| ------------ | --------------- | -----------|
|     v12.6.0* |              v3 | right here |
|      v12.0.0 |              v2 | [/v2/main](https://github.com/autoguru-au/hotchocolate-polymorphic-ids/tree/v2/main) branch |
|      v11.1.0 |              v1 | [/v1/main](https://github.com/autoguru-au/hotchocolate-polymorphic-ids/tree/v1/main) branch |

\* Denotes unexpected binary incompatibility / breaking change in Hot Chocolate
