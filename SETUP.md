# Setup

> **INITIAL OUTLINE: NOT YET USABLE.** There is no application code yet, so there is nothing to install or run. Each section below is a placeholder that will be replaced with accurate, tested instructions as the project is built.

## Prerequisites (planned)

- Git
- .NET 10 SDK
- Node.js (LTS) and npm
- Docker (with Docker Compose) for the local SQL Server container
- An editor or IDE of your choice

Exact versions will be recorded here once they are confirmed.

## Git and GitHub basics

Git keeps a history of every change to the project. GitHub is the website that stores a shared copy of that history. The repository is at <https://github.com/kylepayne0110/inventory-management-app>.

### One-time setup for a new local folder

1. **Start tracking the folder.**

   ```bash
   git init
   ```

   This turns the current folder into a Git repository, so Git begins tracking its history.

2. **Name the branch `main`.**

   ```bash
   git branch -m main
   ```

   A branch is a line of history. This renames the current branch to `main`, the conventional name for the primary one.

3. **Set the author identity for this repository.**

   ```bash
   git config user.name "Your Name"
   git config user.email "your-email@example.com"
   ```

   Every commit records who made it. Leaving out `--global` keeps these settings local to this project only; your other repositories are unaffected. To keep a personal email address private, use the GitHub noreply address shown in your GitHub email settings (it looks like `<id>+<username>@users.noreply.github.com`).

4. **Connect the folder to GitHub.**

   ```bash
   git remote add origin https://github.com/kylepayne0110/inventory-management-app
   ```

   A remote is a copy of the repository hosted elsewhere. `origin` is the usual name for the main one. This command only records the address; it does not upload anything.

### Everyday workflow

```bash
git status
git add <files>
git commit -m "docs: describe the change"
git push
```

- `git status` shows which files are changed, staged, or untracked.
- `git add` stages files, meaning it marks them for inclusion in the next commit.
- `git commit -m "..."` saves the staged changes as a snapshot in the local history, with a short message describing it.
- `git push` uploads new local commits to GitHub.

### Optional: review what is staged before committing

```bash
git diff --cached
git diff --cached --check
git diff --cached --stat
```

"Cached" is another word for "staged". These commands look only at what `git add` has staged:

- `git diff --cached` shows the exact line-by-line changes that will go into the commit.
- `git diff --cached --check` is an optional check that warns about whitespace problems, such as trailing spaces.
- `git diff --cached --stat` is an optional summary listing each changed file and how many lines changed.

### The first push

```bash
git push -u origin main
```

The first time, Git needs to know where `main` should go. `-u` (short for `--set-upstream`) links the local `main` branch to `main` on `origin`, so later pushes can simply use `git push`.

Authentication is handled by GitHub's normal sign-in prompts or your configured credential helper. Never write passwords or access tokens into project files.

## Get the code (to be written)

Cloning instructions (`git clone https://github.com/kylepayne0110/inventory-management-app`) will be finalized here once the app setup steps below are written.

## Start the database (to be written)

SQL Server will run in a local Docker container defined in `compose.yaml`. Instructions, including how to supply the database password through a local environment file that is not committed, will be added when the file exists.

## Run the API (to be written)

Commands for restoring, building, applying database migrations, and starting the .NET API.

## Run the frontend (to be written)

Commands for installing dependencies and starting the React development server.

## Run the tests (to be written)

Commands for running the xUnit test suite.

## Troubleshooting (to be written)

Common problems and fixes will be collected here as they come up.
