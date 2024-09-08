# fbi

> **f**ile **b**ifurcation **i**nterface; high-performance fuzzy diff/patch implementation for .NET.

---

**fbi** is a library for diffing and patching text files for .NET, directly inspired by and originally based on [Chicken-Bones/DiffPatch](https://github.com/Chicken-Bones/DiffPatch).

This project *works*, and, from my testing, it works *well*. That being said, the API is not finalized and is subject to change, and while it is perfectly usable in projects, API guarantees are not guaranteed until a proper release cycle is entered.

This projects iterates over `DiffPatch` significantly, boasting:

- a considerably smaller memory footprint
  - stack allocations are encouraged by making unnecessarily-instanced data static and changing various data structures to structs;
  - no longer holding onto garbage references (allowing the garbage collector to better do its job and avoiding additional heap allocations);
  - minimizing unnecessary allocations by handling well-known situations (including hashing segments of strings without allocating new strings and avoiding allocations per-diff line when serializing patch files).
- and an extremely optimized execution speed.
  - `TokenMapper` boasts the hottest code paths, resolved in part by heavy use of caching;
  - as mentioned before, string slicing is avoided in favor of hashing ranges explicitly;
  - setting knowable capacities to avoid copying.
