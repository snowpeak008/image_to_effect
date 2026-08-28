# VFX Composer Documentation — Internal MVP 0.1.0

This package is currently embedded in the VFX Composer repository at
`project/Packages/com.vfxcomposer.unity`; it is not a standalone registry release.

The repository-level release instructions are at
`docs/release/INSTALL_AND_VERIFY.md`. The frozen Recipe v1, Template Manifest
v1, template-authoring rules, and upgrade boundary are at
`docs/release/RECIPE_MANIFEST_TEMPLATE_V1.md` and
`docs/release/UPGRADE_AND_MIGRATION.md`. AI authors should start at
`docs/ai-workflow/README.md`.

Those paths intentionally remain plain repository paths rather than Package
relative links: they are valid for the embedded-package layout but would not be
valid after exporting this package alone through UPM.
