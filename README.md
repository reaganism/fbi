# fbi

> **f**ile **b**ifurcation **i**nterface; high-performance fuzzy diff/patch implementation for .NET.

---

**fbi** is fuzzy diff library implementing APIs for creating diffs and applying them as patches for .NET, directly inspired by and originally based on [Chicken-Bones/DiffPatch](https://github.com/Chicken-Bones/DiffPatch).

This project *works*, and, from my testing, it works *well*. That being said, the API is not finalized and is subject to change, and while it is perfectly usable in projects, API guarantees are not guaranteed until a proper release cycle is entered.

This projects iterates over `DiffPatch` significantly, greatly improving execution speeds and minimizing memory consumption.
