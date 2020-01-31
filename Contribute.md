# Contributing to the GRAL Dispersion Model
Thank you very much for developing GRAL further or for fixing bugs, so that the entire community can benefit from it!

Do not hesitate to contact the project administrators at the beginning of your work. Changes to GRAL have to go through a complex validation
 process, so it is advantageous if the validation body, which is currently Dietmar Oettl, knows exactly the content of the changes.

## Branch Configuration

```
-- master  : production and bug fixes
-- develop : release ready commits and bug fixes
-- features/feature-xx: always branch from develop and delete after merging to develop
```

- *master* branch is inteded for production release. Keep it simple and easy to rollback
- *develop* branch is for release preparation. Only for release ready commits.


## Recommended Process

If you're developing a **new feature**

1. Create a feature branch from `develop` branch
2. Branch name dependend on your new `feature`
3. When your code is ready for release, pull request to the `develop` branch
4. Delete the feature branch


If you're making a **bug fix**

1. Pull request to the `develop` branch
2. Add an issue tag in the commit message or pull request message

If you're making a **hot fix**, which has to be deployed immediately.
1. Pull request to `develop` **and** `master` branch

## Code of Conduct
This project and everyone participating in it is governed by the our Code of Conduct. By participating, you are expected to uphold this code. 
In the interest of fostering an open and welcoming environment, we as contributors and maintainers pledge to make participation in our project
 and our community a harassment-free experience for everyone, regardless of age, body size, disability, ethnicity, sex characteristics, gender identity 
 and expression, level of experience, education, socio-economic status, nationality, personal appearance, race, religion, or sexual identity and orientation.
Project maintainers are responsible for clarifying the standards of acceptable behavior and are expected to take appropriate and fair corrective action in response to any instances of unacceptable behavior.

Project maintainers have the right and responsibility to remove, edit, or reject comments, commits, code, wiki edits, issues, and other contributions 
that are not aligned to this Code of Conduct, or to ban temporarily or permanently any contributor for other behaviors that they deem inappropriate, 
threatening, offensive, or harmful.

## I don't want to contribute, I just have a question!
Support is provided by the [Technical University of Graz, Austria](http://lampz.tugraz.at/~gral/). 

## Found a Bug?
If you find a bug in the source code, you can help us by submitting an issue to our GitHub Repository. Even better, you can submit a Pull Request with a fix or send us an E Mail.
Please test the bug fix by one ore more projects and document the changes.

## What should I know before I get started?
GRAL is developed on .NetCore 3.1. You can use Visual Studio Code for development across platforms or Visual Studio 2019 in Windows

## Design Decisions
For performance reasons, static jagged arrays and as few classes as possible are used (avoidance of boxing/unboxing). 

## Styleguides
We follow the Microsoft design rules.

### Git Commit Messages
* Use the present tense ("Add feature" not "Added feature")
* Use the imperative mood ("Change array a[] to..." not "Changes array a[] to...")
* Reference issues and pull requests liberally after the first line

