# Sticky To Do

桌面侧边便签待办，使用 Electron 构建。

## 本地运行

```bash
npm install
npm start
```

## 发布自动更新

自动更新使用 GitHub Releases。发布新版时：

1. 修改 `package.json` 里的 `version`。
2. 提交并推送代码。
3. 创建并推送对应标签，例如：

```bash
git tag v1.0.3
git push origin main --tags
```

GitHub Actions 会自动构建 Windows 安装包，并把安装包和 `latest.yml` 上传到 Release。用户安装过 GitHub 版本后，后续会通过应用自动更新。
