# 把这个项目上传到 GitHub

本仓库需要的东西已经备好了：

| 文件 | 作用 |
|---|---|
| `.gitignore` | 忽略 `dist/` 等编译产物，仓库里只放源码和文档 |
| `LICENSE` | MIT 协议（**请把 `<你的名字>` 换成你的名字或 GitHub ID**） |
| `docs/actions-build.yml` | GitHub Actions 配置（**复制到 `.github/workflows/build.yml` 就生效**：推上去后自动用 `csc` 编译，产物可在 Actions 里下载；打 `v*` tag 时自动把 exe 挂到 Release） |

---

## 一、准备 Git（本机已装）

本机 Git 在 `D:\Installation\Git\cmd\git.exe`（已在该用户 PATH 里，新开的终端直接敲 `git` 就能用）。

若换台机器，先装一次：

```powershell
# 有 winget 的话
winget install --id Git.Git -e
# 或者去 https://git-scm.com/download/win 下安装包
```

首次使用设置身份（提交记录里显示的名字/邮箱，**只需做一次**）：

```powershell
git config --global user.name  "你的名字"
git config --global user.email "你的邮箱@example.com"
```

## 二、在本地把仓库建起来并提交

在项目目录 `F:\Repostory\CherryStudioMedia\Workbudy\Software\StickyTodo` 下执行：

```powershell
git init -b main          # 初始化，主分支叫 main
git add -A                # 把源码/文档/配置加进暂存区
git commit -m "feat: 便签 TODO 首个版本（仿微软 To Do 的本地待办小工具）"
git log --oneline         # 确认有一次提交
```

> 提交前可以 `git status` 看一眼：应该只有 `.cs / .ps1 / .md / .png / .ico / .manifest / LICENSE / .gitignore / .github`，**不应该有 `dist/StickyTodo.exe`**（已忽略）。

## 三、在 GitHub 上建一个空仓库

1. 打开 https://github.com/new ；
2. `Repository name` 填 `StickyTodo`（或你喜欢的名字）；
3. 可见性按需选 `Public` / `Private`；
4. **不要**勾选 “Add a README file / .gitignore / license”（本地已经有了，勾了反而要多一步合并）；
5. 点 `Create repository`，复制页面上给出的仓库地址，例如：
   - HTTPS：`https://github.com/<你的用户名>/StickyTodo.git`
   - SSH：`git@github.com:<你的用户名>/StickyTodo.git`

## 四、关联远程仓库并推送

```powershell
git remote add origin https://github.com/<你的用户名>/StickyTodo.git
git push -u origin main
```

### 关于凭据

- **HTTPS**：GitHub 已不接受账号密码，需要 Personal Access Token（PAT）：
  打开 https://github.com/settings/tokens → `Generate new token (classic)` → 勾上 **`repo`** → 生成后复制；
  在 `git push` 提示输入密码时，粘贴这串 token（用户名填你的 GitHub 用户名）。Windows 上装了 Git Credential Manager 的话，第一次输入后会记住。
- **SSH**（免密，推荐长期用）：
  ```powershell
  ssh-keygen -t ed25519 -C "你的邮箱@example.com"     # 一路回车
  Get-Content "$env:USERPROFILE\.ssh\id_ed25519.pub"  # 复制输出
  ```
  把公钥粘到 https://github.com/settings/keys → `New SSH key`，然后：
  ```powershell
  git remote set-url origin git@github.com:<你的用户名>/StickyTodo.git
  git push -u origin main
  ```

## 五、以后日常更新

```powershell
git status                 # 看改了哪些文件
git add -A
git commit -m "fix: 图标与文字对齐"
git push
```

## 五之二、启用自动编译（可选，30 秒）

GitHub 只执行 `.github/workflows/` 目录下的配置；而这个目录里的文件属于 "workflow 文件"，用**没有 `workflow` 权限**的 token 推送会被 GitHub 拒（报 `refusing to allow a Personal Access Token to create or update workflow`）。
所以本仓库把它放在了 `docs/actions-build.yml`，启用方式任选其一：

**方式 A：网页端添加（不用改 token）**
1. 打开 `https://github.com/<你的用户名>/StickyTodo/new/main`；
2. 文件名填 `.github/workflows/build.yml`；
3. 把 `docs/actions-build.yml` 的内容整段粘进去，提交。

**方式 B：给 token 加权限后再推**
1. 打开 https://github.com/settings/tokens ，点开那个 token，勾上 **`workflow`**，点 `Update token`；
2. 本地执行：
   ```powershell
   git checkout -b ci
   git mv docs/actions-build.yml .github/workflows/build.yml   # 若目录不存在用 New-Item 建
   git commit -m "ci: 启用 GitHub Actions 自动编译"
   git push origin ci      # 然后在网页上发起 Pull Request 合并
   ```

启用后，**Actions** 页能看到每次推送的编译结果；打 `v*` tag 时会把 `StickyTodo.exe` 传到 Release。
## 六、发一个可下载的版本（Release）

仓库里不放 exe，发布走 Release（读者点一下就能下载）。CI 已经配好，只要打个 tag：

```powershell
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions 会自动编译，并把 `StickyTodo.exe` 挂到 `v1.0.0` 这个 Release 上（在仓库的 **Actions** 页能看到构建过程，**Releases** 页看到成品）。

## 七、几个注意点

- **源码是 UTF-8 无 BOM 的中文**，`build.ps1` 里已经加了 `/codepage:65001`；Git 在 Windows 上默认不会乱改编码，不用额外配置。
- **`docs/` 里的截图**一共约 1.3 MB，远小于 GitHub 单文件 100 MB 的限制，可以直接进仓库；博客稿 `docs/StickyTodo-博客稿.md` 里用的是相对路径引用，拷到博客时记得把 4 张截图一起放上去。
- **数据文件不在仓库里**：程序把数据写在 `%APPDATA%\StickyTodo\todos.json`，和仓库无关，不会误传个人待办。
- 如果 `git push` 报 `failed to push some refs`，通常是远程仓库建的时候勾了 README，先 `git pull --rebase origin main` 再推。
- 想让仓库更完整，可以再补：`CHANGELOG.md`、英文版 README（`README.en.md`）、以及把 `docs/` 里的截图也放进 README 顶部做展示。
