# Sticky To Do

桌面侧边便签待办，使用 Electron 构建。

## 下载使用

到 GitHub Releases 下载最新版：

- `便签-版本号-win-x64.zip`：绿色版，下载后解压，直接运行里面的 `便签.exe`。
- `便签-版本号-win-x64.exe`：安装版，支持更完整的自动更新体验，但未签名时可能被 Windows SmartScreen 拦截。

如果 Windows 拦截安装版，优先使用 zip 绿色版。

## 本地运行

```bash
npm install
npm start
```

## 发布新版

自动更新使用 GitHub Releases。发布新版时：

1. 修改 `package.json` 里的 `version`。
2. 提交并推送代码。
3. 创建并推送对应标签，例如：

```bash
git tag v1.0.5
git push origin main --tags
```

GitHub Actions 会自动构建 Windows 安装包、绿色版 zip 和自动更新元数据，并上传到 Release。
