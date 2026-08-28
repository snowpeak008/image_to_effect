# Frost Module Generation Record

工具：Codex 内置 image generation  
日期：2026-08-23  
Reference：`docs/vfx-reviews/frost_impact_2d/TARGET_EFFECT_v1.png`（仅风格/颜色参考）

## Final prompt set

1. **Broken Ring**：生成一个完整、居中、无遮挡的圆形破碎冰冲击环；奇幻游戏 VFX 画法，白青高光、深蓝裂纹、局部冰雾、不规则缺口和尖刺；透明空心中心；禁止中心爆发、大型放射冰晶、规则霓虹管、文字和水印。
2. **Shard Variants**：生成严格 3×2 网格中的六个互不重叠冰晶变体；每格完整、竖直、根部在下、尖端朝 `+Y`；半透明切面、白青边缘和蓝色内部裂纹；透明背景；禁止环、雾、粒子、重复副本、文字和水印。
3. **Mist Ring**：生成一个完整、居中、空心的冰雾光环；柔和蓝白冻雾、不均匀密度、断续弧和细霜噪声；透明背景；禁止硬质粗圆线、中心闪光、大冰晶、雪花图标、文字和水印。

工具实际返回 RGB 并烘焙浅色棋盘格，未达到请求的透明输出。原始文件保存在 `RawGenerated/`，不得直接进入 Runtime；确定性清理和打包过程见 `tools/vfx/build_frost_family_atlases.py`，最终 hash 见 Atlas Layout contract。
