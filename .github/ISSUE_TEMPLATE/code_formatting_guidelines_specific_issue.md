---
name: Formatting or code style guidelines issue
about: Discuss editor / tooling formatting concerns and code style guidelines
title: ''
labels: [style-guide, under-discussion]
assignees: '@nojaf,@KathleenDollard,@dsyme'
---

Depending the concern being discussed, erase the other top sections and review/tick what applies the most.

# Is your request about a feature in code formatting (using fantomas, .editorconfig, editors with formatting options)?

Formatting infrastructure decision checklist:
* [ ] If this is a code formatter discussion, please suggest a detailed list of settings names and their default values, with their behaviour
* [ ] the choice for tooling support should be based on ease of implementation in code formatter
* [ ] the choice for tooling output should be based on
  * [ ] conserving original formatting but for specific predicate that makes code significantly less maintainable
  * [ ] maximizing uniformity of a specific construct across a particular codebase
* [ ] I would like to contribute testing the feature implementation as end user
* [ ] I would like to contribute unit tests so the tooling implementation covers my use cases
* [ ] I would like to contribute implementation
  * [ ] pro-bono
  * [ ] sponsored
  * [ ] sponsoring
* [ ] The implementation requires the several options upfront (rather than just the most "agreed upon") [^1]
* [ ] This is something that is common place in other langauges (add items nested under this list with one language per line)
* [ ] There are examples of codeformatters settings in other languages / tooling available (add items nested under this list with one link per line), add screenshots to the description
* [ ] There are examples of codeformatters implementation available (add items nested under this list with one link per line)

[^1] this is generally evaluated by the person implementing and the entity potentially sponsoring the work

# Is your request about [official style guide](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/) update?

Style guide update adoption checklist:
* [ ] I have submitted a PR / Draft on docs repository
* [ ] A PR was merged about it on docs repository and I want to bring it to attention of the community
* [ ] I want the community to debate and share perspectives
* [ ] This is higher level concern than formatting:
  * [ ] soundness/robustness of F# code
  * [ ] performance concerns
  * [ ] approachability of code towards:
     * [ ] people debutting with programming languages
     * [ ] seasoned developers
