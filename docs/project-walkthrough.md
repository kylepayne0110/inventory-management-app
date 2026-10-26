# Project Walkthrough

This is the build log for the inventory management app. It is updated at the end of each section of the project.

Each entry will capture:

- **What we built**: the concrete result of the section.
- **Why**: the reasoning behind it.
- **Important commands and decisions**: what was run, and what alternatives were considered.
- **Plain-language explanations**: short, non-jargon descriptions of the technologies involved, so a nontechnical reader can follow along.

See [milestones.md](milestones.md) for the list of sections and their progress and [architecture.md](architecture.md) for the design.

---

## Section 0: Foundation (complete)

**Status:** Section 0, Foundation, is complete. The documentation and repository foundation are in place; no application features are implemented.

**What we built.** Documentation and repository configuration files only: `README.md`, `SETUP.md`, `.gitignore`, `.editorconfig`, and the three files in `docs/`. There is no application code yet.

**Why.** Writing down the purpose, planned stack, and rules before coding gives later work a clear target and keeps the project understandable to readers who are not developers.

**Decisions.**

- Transfers will preserve the original receipt date, so stock age stays accurate.
- Issuing stock will not use FIFO; users will select which batch to issue from.
- SQL Server's data will be kept in a named Docker volume.
- The app will run locally only; there will be no deployment and no integrations.

**Plain-language notes.**

- *`.gitignore`* tells Git which files to leave out of the project history, such as passwords, downloaded libraries, and build output.
- *`.editorconfig`* tells editors to use consistent formatting (indentation, line endings) so code looks the same no matter who edits it.

**Repository.** The Git repository was initialized on the `main` branch and pushed to GitHub at <https://github.com/kylepayne0110/inventory-management-app>. The foundation commit is `0177c5e` ("docs: add inventory project foundation").

**Git steps in Section 0.** These are the steps that put the documentation foundation on GitHub:

1. **Initialization (`git init`).** Told Git to start tracking the project folder. Before this, the folder was ordinary files with no history.
2. **Branch rename (`git branch -m main`).** Named the main line of history `main`.
3. **Local author identity (`git config user.name` and `git config user.email`).** Recorded who is making commits. Without `--global`, this applies only to this project. A GitHub noreply email was used so the personal address stays private.
4. **First commit (`0177c5e`).** After checking status and staging the files (`git status`, `git add`, optionally `git diff --cached` to review), `git commit -m "docs: add inventory project foundation"` saved the first snapshot of the documentation and configuration files.
5. **Remote connection (`git remote add origin https://github.com/kylepayne0110/inventory-management-app`).** Linked the local folder to the empty GitHub repository. Nothing was uploaded yet.
6. **Successful push (`git push -u origin main`).** Uploaded the commit to GitHub. The `-u` flag linked local `main` to the remote `main`, so later pushes only need `git push`.

See the "Git and GitHub basics" section of [SETUP.md](../SETUP.md) for a reusable version of these steps. This work only covers documentation and version tracking; none of the app's features are built.

**What is not done.** `SETUP.md` is still an initial outline and will be completed as the app is built. All later sections are not started.
