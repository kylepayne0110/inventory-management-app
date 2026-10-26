# Setup

> **PARTIALLY AVAILABLE.** Only the API foundation (Section 1) exists. You can restore, build, run, and test the API as described below. The database and frontend steps are not yet available and remain placeholders.

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

## Start the database (not yet available)

SQL Server will run in a local Docker container defined in `compose.yaml`. Instructions, including how to supply the database password through a local environment file that is not committed, will be added when the file exists.

## Run the API

Run these from the repository root. The .NET 10 SDK is required.

```bash
dotnet restore InventoryManagement.slnx
dotnet build InventoryManagement.slnx
dotnet run --project backend/Inventory.Api
```

- `dotnet restore` downloads the libraries the projects need.
- `dotnet build` compiles the API and the test project.
- `dotnet run` starts the API. It listens on <http://localhost:5127> (set in `backend/Inventory.Api/Properties/launchSettings.json`). Stop it with Ctrl+C.

With the API running (in the Development environment, which `dotnet run` uses by default):

- Health check: <http://localhost:5127/health> returns `{"status":"Healthy"}`.
- Swagger UI (interactive API documentation): <http://localhost:5127/swagger>
- OpenAPI document (raw JSON): <http://localhost:5127/openapi/v1.json>

Database migrations are not part of this step yet; there is no database.

## Run the frontend (not yet available)

Commands for installing dependencies and starting the React development server.

## Run the tests

```bash
dotnet test InventoryManagement.slnx
```

This builds the solution and runs the xUnit tests in `tests/Inventory.Api.Tests`. Currently there is one integration test, which starts the API in memory and checks that `/health` returns 200 OK with a `Healthy` status. The API does not need to be running separately.

## Troubleshooting (to be written)

Common problems and fixes will be collected here as they come up.
