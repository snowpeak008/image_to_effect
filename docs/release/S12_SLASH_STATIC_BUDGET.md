# S12 Slash v2 static budget

结论：**静态预检，非真机认证。** This is the isolated Slash v2 live budget evaluator, not a CPU/GPU, Draw Call, memory, or device-performance measurement.

| Recipe | actual peak particles | actual particle systems | actual materials | actual transparent renderers | duration (s) | peak limit | system limit | material limit | renderer limit |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| slash_3d_stylized | 37 | 3 | 4 | 7 | 0.45 | 48 | 4 | 5 | 7 |

The real `S12SlashBudgetCalculator` evaluates both `mobile_medium` and `pc_editor` against the v2 fixed limits: 48 peak particles, 4 particle systems, 5 materials, and 7 transparent renderers. The table's actual values are recomputed from enabled modules and de-duplicated material GUIDs by the S12 release test; this is not a claim of device certification or measured frame time.
