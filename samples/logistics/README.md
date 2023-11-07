# Warehousing Sample

In this sample, Manifest is given two types of resources to handle: `Locations` and `Stocks`. A `location` is a very simple structure with just one field identifying the physical space in a warehouse that can hold a `stock`. A `stock` represent an actual amount of a specific material that is located in a `location`.

## Controller

The controller watches stocks and locations for changes. All changes are gathered to build an in-memory map of both `locations` and `stocks` and look for stocks placed on non-existing locations. It could then also act on this knowledge by creating a location on-the-fly.