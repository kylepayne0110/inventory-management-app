# Setup

> **PARTIALLY AVAILABLE.** The API (Section 1) and the local database connection (Section 2, in progress) exist. You can start SQL Server, run the API, and test it as described below. The frontend steps are not yet available.

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

## Start the database

SQL Server runs in a Docker container defined in `compose.yaml`. All commands run from the repository root.

Plain-language terms:

- **Docker Compose** reads `compose.yaml`, which describes the containers to run, so one command starts them the same way every time.
- A **named volume** (`sqlserver-data`) is storage managed by Docker that outlives the container. You can stop or recreate the container and the database contents remain.
- **EF Core** (Entity Framework Core) is the library the API uses to talk to the database using C# objects instead of hand-written SQL.
- A **DbContext** (`InventoryDbContext`) is the C# object representing a session with the database. It has no tables yet; Section 3 adds them.
- **User Secrets** is a .NET feature that stores development secrets in a file in your home folder, outside the repository, so passwords are never committed.

### 1. Create the `.env` file

```bash
cp .env.example .env
```

Open `.env` in an editor and replace the placeholder with your own password: at least 8 characters with upper case, lower case, and a digit, and without `;` or `=`. `.env` is ignored by Git; never commit it.

### 2. Start and stop SQL Server

```bash
docker compose up -d      # start in the background (first run downloads the image)
docker compose ps         # wait until the status says "healthy"
docker compose stop       # stop, keeping the data
docker compose down       # remove the container, keeping the data volume
docker compose down -v    # remove the container AND delete the data (destructive)
```

### 3. Configure User Secrets

Load the password from `.env` into your shell, then store the connection string in User Secrets. The password is never written to a tracked file.

```bash
set -a; source .env; set +a
dotnet user-secrets set "ConnectionStrings:InventoryDb" "Server=localhost,1433;Database=master;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;Encrypt=True" --project backend/Inventory.Api
```

`Database=master` is the built-in system database. It is used for now because the application database, `InventoryManagement`, does not exist yet, and `master` always exists, so `/health/db` can confirm SQL Server is reachable. In Section 3, before running the first EF Core migration, change `Database=master` to `Database=InventoryManagement` in the same User Secrets command. Then run the migration: EF Core's migration command creates the missing database and applies the schema. The same setting can instead be supplied as the environment variable `ConnectionStrings__InventoryDb`.

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

With the API running (in the Development environment, which `dotnet run` uses by default), check both health endpoints from another terminal:

```bash
curl -i http://localhost:5127/health      # API is up: 200 {"status":"Healthy"}
curl -i http://localhost:5127/health/db   # SQL Server reachable: 200 {"status":"Healthy"}
```

If SQL Server is stopped or the secret is missing, `/health/db` returns 503 `{"status":"Unhealthy"}`. It deliberately gives no further detail, so no credentials leak.

Other pages:

- Swagger UI (interactive API documentation): <http://localhost:5127/swagger>
- OpenAPI document (raw JSON): <http://localhost:5127/openapi/v1.json>

Database migrations are not part of this section. The first migration is created in Section 3, when the first real tables are introduced.

## Run the frontend (not yet available)

Commands for installing dependencies and starting the React development server.

## Run the tests

```bash
dotnet test InventoryManagement.slnx
```

This builds the solution and runs the xUnit tests in `tests/Inventory.Api.Tests`. Currently there are two tests. One starts the API in memory and checks that `/health` returns 200 OK with a `Healthy` status. The other checks that `/health/db` returns 503 `Unhealthy`, with no password or host in the response, when SQL Server is unreachable. Neither needs the API or SQL Server running.

## Troubleshooting (to be written)

Common problems and fixes will be collected here as they come up.
