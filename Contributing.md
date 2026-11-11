# Contributing to the GRAL Dispersion Model
Thank you very much for developing GRAL further or for fixing bugs, so that the entire community can benefit from it!

Do not hesitate to contact the project administrators at the beginning of your work. Changes to GRAL have to go through a complex validation
 process, so it is advantageous if the validation body, which is currently Dietmar Oettl, knows exactly the content of the changes.

## Branch Configuration

```
-- main    : production and bug fixes
-- V2XXX   : release ready commits and bug fixes
-- features/feature-xx: always branch from develop and delete after merging to develop
```

- *main*   branch is inteded for production release. Keep it simple and easy to rollback
- *V2XXX*  branch is for release preparation. Only for release ready commits.


## Recommended Process

If you're developing a **new feature**

1. Create a feature branch from `V2XXX` branch
2. Branch name dependend on your new `feature`
3. When your code is ready for release, pull request to the `V2XXX` branch
4. Delete the feature branch


If you're making a **bug fix**

1. Pull request to the `V2XXX` branch
2. Add an issue tag in the commit message or pull request message

If you're making a **hot fix**, which has to be deployed immediately.
1. Pull request to `V2XXX` **and** `main` branch

## I don't want to contribute, I just have a question!
Support is provided by the [Technical University of Graz, Austria](http://lampz.tugraz.at/~gral/). 

## Found a Bug?
If you find a bug in the source code, you can help us by submitting an issue to our GitHub Repository. Even better, you can submit a Pull Request with a fix or send us an E Mail.
Please test the bug fix by one ore more projects and document the changes.

## What should I know before I get started?
GRAL is developed on .Net10. You can use Visual Studio Code for development across platforms or Visual Studio in Windows

## Design Decisions
For performance reasons, static jagged arrays and as few classes as possible are used (avoidance of boxing/unboxing). 

## Styleguides
We follow the Microsoft design rules.

### Git Commit Messages
* Use the present tense ("Add feature" not "Added feature")
* Use the imperative mood ("Change array a[] to..." not "Changes array a[] to...")
* Reference issues and pull requests liberally after the first line

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update
- [ ] Code refactoring / Cleanup

## AI Disclosure & Verification
- [ ] **AI-Assisted:** This Pull Request contains code generated or assisted by AI tools (e.g., GitHub Copilot, ChatGPT, Claude).
- [ ] **Human-Only:** This Pull Request was written entirely without AI generation.

*If AI-assisted, I confirm that:*
- [ ] I have reviewed every line of the generated code, understand its logic, and verify its correctness.
- [ ] I have verified that the AI did not introduce legacy, insecure, or hallucinated APIs.

## Quality Assurance Checklist
- [ ] **Local Build:** The project builds successfully locally with zero warnings (`dotnet build` with `TreatWarningsAsErrors`).
- [ ] **Nullable Safety:** No new compiler warnings regarding nullable reference types have been introduced.
- [ ] **Documentation:** Code comments and public API documentation have been updated accordingly.

