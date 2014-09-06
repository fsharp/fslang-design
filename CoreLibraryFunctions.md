
# F# 4.0+ design for regularizing and extending the  List, Array and Seq modules

There is an approved [proposal](https://fslang.uservoice.com/forums/245727-f-language/suggestions/5663997-make-fsharp-core-collection-functions-for-list-ar) to make the functions defined in the List, Array and Seq modules in FSharp.Core.dll more regular.

The overall status of F# Future Language Design Items [can be found here](https://github.com/fsharp/FSharpLangDesign/blob/master/Status.md).

New functions added in this proposal: splitAt, contains, findBack, tryFindBack, findIndexBack, tryFindIndexBack, item, tryItem, indexed, mapFold, mapFoldBack, tryLast, tryHead.

If you would like to work on one or more of these function implementations, please [edit and submit a PR to this document](https://github.com/dsyme/FSLangDesignGists/edit/master/CoreLibraryFunctions.md) by
adding an entry to column "assigned to" indicating you're willing to code, test and submit the the functions to
[Visual F# CodePlex Open Git Repo branch "fsharp4"](http://visualfsharp.codeplex.com).  If you have questions, please tweet @dsyme, or raise an issue in this
forum, or discuss on fslang.uservoice.com, link above.  General guidelines and best practices to keep in mind when developing and testing these functions can be found [here](https://visualfsharp.codeplex.com/wikipage?title=Implementing%20FSharp.Core%20collection-processing%20functions).

The F# 2.x and 3.x philosophy for these functions was somewhat irregular. The majority of functions (e.g. map, filter, groupBy, averageBy)
were defined for Seq, but some were not present on List and Array (e.g. groupBy).  This leads to awkward code where Seq-producing functions
are used even when List is an input. Seq.groupBy is a particular example.

Also, some functions were not defined on Seq, even though they exist in List or Array.

The proposal below is to complete the matrix for List, Array and Seq w.r.t. functional collection functions.

### Completion

This work is only completed when all rows have a status of ":)".  The library updates will only be done when all PRs are ready (or features are dropped).

Status column - 

* :)  - reviewed and ready to be pulled
* :/  - reviewed but changes needed
* (empty) - no PR or not reviewed

## Regular functional functions

| Function   | Comment   | List      | Array     | Seq      |   PR |    Status      |
|:-----------|:----------|:---------:|:---------:|:--------:|:--------:|:--------:|
| append     |           |     o     |    o      |    o     |   done       |    :)      |
| average    |           |      o    |        o  |      o   |   done       |  :)        |
| averageBy  |           |    o      |      o    |    o     |   done       |  :)        |
| contains   |   new     |   ADD       |     ADD     |   ADD      |    [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/mexx24/visualfsharp/contribution/7030)  | :/  |
| choose     |           |   o       |     o     |   o      |   done       |  :)        |
| collect    |           |  o        |      o    |      o   |   done       |  :)        |
| compareWith|           |  ADD      |     ADD   |     o    |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7040)        | :)  |
| concat     |           |     o     |       o   |     o    |   done       | :)         |
| countBy    |           |  ADD      |     ADD   |      o   |    [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7079)        | :)  |
| distinct   |           |   ADD     |     ADD   |     o    |     [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7047)       |  :)|
| distinctBy |           |    ADD    |    ADD    |    o     |    [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7048)       |  |
| empty      |           |    o      |    o      |      o   |   done       |   :)       |
| exactlyOne |           |    ADD    |    ADD    |        o |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7038)       |  |
| exists     |           |     o     |       o   |     o    |   done       |   :)       |
| exists2    |           |    o      |        o  |      o   |   done       |   :)       |
| filter     |           |   o       |     o     |     o    |   done       |     :)     |
| find       |           |   o       |     o     |     o    |   done       |     :)     |
| findBack       |    new       |   ADD       |     ADD     |     ADD    |    [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/PatrickMcDonald/visualfsharp/contribution/7369)  | :/    |
| findIndex  |           |  o        |      o    |      o   | done         |     :)     |
| findIndexBack  |  new           |  ADD        |      ADD    |      ADD   |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/PatrickMcDonald/visualfsharp/contribution/7369)  | :/     |
| fold       |           |     o     |     o     |     o    | done         |     :)     |
| fold2      |           |   o       |    o      |     ADD  |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7075)       |  |
| foldBack   |           |   o       |    o      |   ADD    |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/PatrickMcDonald/visualfsharp/contribution/7293)        | |
| foldBack2  |           |   o       |   o       |    ADD   |          |          |
| forall     |           |   o       |  o        |     o    |    done      |   :)       |
| forall2    |           |  o        |   o       |      o   |   done       |     :)     |
| groupBy    |           |    o      |       o   |    ADD   |  [PR](https://github.com/fsharp/FSharpLangDesign/edit/master/CoreLibraryFunctions.md)       |  |
| head       |           |   o       |    ADD    |   o      |  [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7069)       |  |
| indexed       |   new, signature ``indexed: C<T> -> C<int*T>``        |   ADD       |    ADD      |     ADD    |          |          |
| init       |           |   o       |    o      |     o    |   done       |   :)       |
| isEmpty    |           |    o      |     o     |      o   |   done       |     :)     |
| item    |   New, see note. SIgnature ``int -> C<'T> -> 'T``        |      ADD    | ADD       |  ADD       |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/mexx24/visualfsharp/contribution/7097)  |     |
| iter       |           |   o       |      o    |     o    |   done       |     :)     |
| iter2      |           |    o      |       o   |    o     |   done       |       :)   |
| iteri      |           |    o      |       o   |    o     |   done       |     :)     |
| iteri2     |           |   o       |      o    |   ADD    |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/PatrickMcDonald/visualfsharp/contribution/7243)     |  |
| last       |           |   ADD     |    ADD    |     o    |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7046)      |  |
| length     |           |   o       |    o      |     o    |   done       |   :)       |
| map        |           |    o      |     o     |      o   |   done       |     :)     |
| map2       |           |   o       |    o      |     o    |   done       |     done     |
| map3       |           |   o       |    ADD    |   ADD    |       [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/andrew_khmylov/fsharp/contribution/7115)      |  |
| mapi       |           |   o       |    o      |     o    |   done       |     :)     |
| mapi2      |           |  o        |   o       |    ADD   |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/PatrickMcDonald/visualfsharp/contribution/7247)     |  |
| mapFold       | New, map + fold, with signature ```mapFold : ('State -> 'T -> 'U * 'State) -> 'State -> C<'T> -> C<'U> * 'State``` e.g. [see here](https://github.com/fsharp/fsharp/blob/8c82d57a6e8cc131740316b00f199d9d48072346/src/absil/illib.fs#L77)          |   ADD       |    ADD      |     ADD    |          |           |
| mapFoldBack       | New, map + fold, with signature ```mapFoldBack : ('T -> 'State -> 'U * 'State) -> C<'T> -> 'State -> C<'U> * 'State``` |   ADD       |    ADD      |     ADD    |          |           |
| max        |           |    o      | o         |  o       |   done       |     :)     |
| maxBy      |           |    o      | o         |    o     |   done       |     :)     |
| min        |           |  o        |         o |  o       |   done       |     :)     |
| minBy      |           |    o      |   o       |    o     |   done       |     :)     |
| nth        |  see note         |      long-term deprecate, see note    | long-term deprecate, see note       |  long-term deprecate, see note   |    [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/mexx24/visualfsharp/contribution/7097)  |  |
| pairwise   |           |     ADD   |    ADD    |     o    |     [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7045)       |    |
| permute    |           |    o      |       o   |    ADD   |    [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/PatrickMcDonald/visualfsharp/contribution/7341)   | :) | 
| pick       |           |     o     |        o  |     o    |   done       |     :)     |
| reduce     |           |     o     |        o  |     o    |   done       |     :)     |
| reduceBack |           |    o      |         o |      ADD |     [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/PatrickMcDonald/visualfsharp/contribution/7293)        |  |
| replicate  |           |     o    |    ADD    |   ADD    |        [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7060)      |   |
| rev        |           |    o      |   o       |    ADD   |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/PatrickMcDonald/visualfsharp/contribution/7340)      |  |
| scan       |           |     o     |      o    |     o    | done         |   :)       |
| scanBack   |           |     o     |    o      |   ADD    |          |          |
| singleton  |           |    ADD    |     ADD   |    o     |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7041)      |  |
| skip       |           |   ADD   |      ADD     |       [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/PatrickMcDonald/visualfsharp/contribution/7258)  |   :)   |
| skipWhile  |           |  ADD      |     ADD   |    o     |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/PatrickMcDonald/visualfsharp/contribution/7282)      |  |
| sort       |           | o         |    o      |     o    |    done      |   :)       |
| sortBy     |           |   o       |      o    |     o    |   done       |   :)       |
| sortWith  |           |    o      |    o      |  ADD     |          |          |
| sortDescending  |           |    ADD      |    ADD      |  ADD     |  [@richardadalton](https://twitter.com/richardadalton) [UserVoice](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6237671-add-sortdescending-to-seq-list-and-array)       |  |
| sortByDescending  |           |    ADD      |    ADD      |  ADD     | [@richardadalton](https://twitter.com/richardadalton)   [UserVoice](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6237671-add-sortdescending-to-seq-list-and-array)       |  |
| sum        |           |    o      |   o       |   o      |   done       |   :)       |
| sumBy      |           |    o      |   o       |   o      |   done       |    :)      |
| tail       |           |    o      |  ADD      |  ADD     | [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/bogdangaliceanu/fsharp/contribution/7389)      |          |
| take       |           |    ADD    |   ADD     |  o       |  [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7049)       |  |
| takeWhile  |           |    ADD    |  ADD      | o        |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7050)       |  |
| truncate   |           |    ADD    | ADD       |  o       |          |          |
| tryFind    |           |    o      |  o        |  o       |   done       | :)         |
| tryFindBack       |    new       |   ADD       |     ADD     |     ADD    |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/PatrickMcDonald/visualfsharp/contribution/7369)  |  :/   | 
| tryFindIndex |         |    o      | o         | o        |   done       | :)         |
| tryFindIndexBack       |    new       |   ADD       |     ADD     |     ADD    |   [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/PatrickMcDonald/visualfsharp/contribution/7369) |   :/    |
| tryHead    |    new       |    ADD      |  ADD        |  ADD       |    [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/rodrigovidal/visualfsharp/contribution/7057)     |  |
| tryItem    |   new         |      ADD    | ADD       |  ADD       |    [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/mexx24/visualfsharp/contribution/7113)     |  |
| tryLast    |    new       |    ADD      |  ADD        |  ADD       |         [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/simonhdickson/visualfsharp/contribution/7263)      |  |
| tryPick    |           |    o      |  o        | o        |   done       | :)         |
| unfold     |           |    ADD    | ADD       |  o       |          |          |
| where      | syn. filter |  ADD    |  ADD      |  o       |  [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7037)        |  |
| windowed   |           |    ADD    |  ADD      |  o       |          |          |
| zip        |           |    o      |  o        |  o       |   done       |   :)       |
| zip3       |           |    o      |  o        |  o       |   done       |   :)       |

Note: In F# 3.0 Seq.where was defined as a synonym for Seq.filter, mainly due to the use of "where" in query expressions. Given
it already  exists as a synonym (= decision made) it seems sensible to just complete the matrix and define List.where and Array.where as well.

Note: In F# 3.x, ``nth`` is defined with inconsistent signatures for Array and List.  The proposal above replaces ``nth`` by
``item`` and would eventually deprecate ``nth`` (with a message to say 'please use Seq.item'. It also adds a corresponding ``tryItem``.  Both ``item`` and ``tryItem``  would take the integer index as the first parameter.



## Regular functional operators producing two or more output collections

These operators are not defined for Seq.* for performance reasons because using them would require iterating the input sequence twice.

| Note it is arguable that if these are not defined, then Seq.tail, Seq.skip and Seq.skipWhile should also not be defined, since
| they implicitly skip inputs and can be a performance trap, especially when used recursively.

| Function   | Comment   | List      | Array     | Seq      |   PR       |    Status      |
|:-----------|:----------|:---------:|:---------:|:--------:|:--------:|:--------:|
| partition  |           |    o      |       o   |    n/a    |  ---        |   :)       |
| splitAt  |      new, taking index     |    ADD      |    ADD      |  n/a   | [PR](https://visualfsharp.codeplex.com/SourceControl/network/forks/forki/fsharp/contribution/7052)      |  |
| unzip      |           |    o      |   o       | n/a       |  ---        |  :)        |
| unzip3     |           |    o      |  o        | n/a       |  ---        |  :)        |

## Mutation-related operators

| Function   | Comment   | List      | Array     | Seq      |   xxxxxxxxxxxxxxxxxxx       |    xxxxxxxxxxxxxxx      |
|:-----------|:----------|:---------:|:---------:|:--------:|:--------:|:--------:|
| blit       |           |     n/a   |   o       |   n/a    |          |          |
| copy       |           |   n/a     |     o     |     n/a  |          |          |
| create     |           |   n/a     |      o    |    n/a   |          |          |
| fill       |           |   n/a     |     o     |     n/a  |          |          |
| get        |           |    n/a    |     o     |  n/a     |          |          |
| set        |           |    n/a    |   o       |    n/a   |          |          |
| sortInPlace  |         |    n/a    |    o      |   n/a    |          |          |
| sortInPlaceBy  |       |    n/a    |   o       |   n/a    |          |          |
| sortInPlaceWith  |     |    n/a    |    o      |   n/a    |          |          |
| sub        |           |    n/a    |   o       |  n/a     |          |          |
| zeroCreate |           |    n/a    |  o        |n/a       |          |          |

## Conversion operators

| Function   | Comment   | List      | Array     | Seq      |   xxxxxxxxxxxxxxxxxxx       |    xxxxxxxxxxxxxxx      |
|:-----------|:----------|:---------:|:---------:|:--------:|:--------:|:--------:|
| ofList     |           |   n/a     |    o      |   o      |          |          |
| ofArray    |           |      o    | n/a       |    o     |          |          |
| ofSeq      |           |      o    |       o   |    n/a   |          |          |
| toList     |           |    n/a    |  o        | o        |          |          |
| toArray    |           |    o      |  n/a      |  o       |          |          |
| toSeq      |           |    o      | o         | n/a      |          |          |

## On-demand or IEnumerable computation operators

| Function   | Comment   | List      | Array     | Seq      |   xxxxxxxxxxxxxxxxxxx       |    xxxxxxxxxxxxxxx      |
|:-----------|:----------|:---------:|:---------:|:--------:|:--------:|:--------:|
| cache      |           |    n/a    |    n/a    |   o      |          |          |
| cast       |           |   n/a     |   n/a     |   o      |          |          |
| delay      |           |    n/a    |    n/a    |    o     |          |          |
| initInfinite |         |    n/a    |   n/a     |    o     |          |          |
| readonly   |           |     n/a   |      n/a  |   o      |          |          |
