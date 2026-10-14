# 用户提供的灯笼

`lantern.blend` 是用户最新附件的未修改副本，源文件与保存副本的 SHA-256 已核对一致。只使用该文件的灯笼，不导入同目录的 AK，也没有导入之前 Maya 文件中的火柴。

原模型约 5 万三角面，含横向握柄、框架与纸质灯罩及打包 PBR 贴图。导出脚本 `Tools/Art/import_user_lantern.py` 保留原 UV、颜色/金属粗糙度/法线贴图，规范化总高为 0.40 米，并优化至 12000 三角面。运行时比例为 0.75。

纸面分为独立发光子网格，原框架仍使用源 PBR 材质。灯罩存在反向面，Unity 使用双面渲染以避免开启发光后仍看不到纸面。发光材质默认关闭，由持有者的材质属性块控制，不修改所有灯笼共享的材质状态。

握柄和光源坐标见 `import-manifest.json`，资源位于 `Assets/Resources/FishingUserTools`。配置入口为 `FishingUserLanternAssets.Build`。来源模型的发布许可仍由用户确认，不新增第三方授权声明。
