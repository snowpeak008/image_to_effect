# Retired — do not dispatch

This first S12 Slash AI export is invalid for dispatch. Its `contract.recipe.phases[].modules[].parameters` used parameter declarations rather than the Recipe v2 required bare numeric values. It is retained only as an audit record because no AI agent was dispatched from it. A future versioned `s12-slash-v2` freeze must separate a parser-valid `recipeSkeleton` (bare defaults) from a template catalog of declarations.
