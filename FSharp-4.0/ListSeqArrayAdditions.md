## F# 4.0 Speclet: Regularizing and extending the List, Array and Seq modules

[User Voice](https://fslang.uservoice.com/forums/245727-f-language/suggestions/5663997-make-fsharp-core-collection-functions-for-list-ar)

There is an approved proposal to make the functions defined in the `List`, `Array`, and `Seq` modules in FSharp.Core.dll more regular and to add some new functions. 

New functions added in this proposal: `splitAt`, `contains`, `findBack`, `tryFindBack`, `findIndexBack`, `tryFindIndexBack`, `item`, `tryItem`, `indexed`, `mapFold`, `mapFoldBack`, `tryLast`, `tryHead`.

### Background

The F# 2.x and 3.x philosophy for these functions was somewhat irregular. The majority of functions (e.g. `map`, `filter`, `groupBy`, `averageBy`) were defined for `Seq`, but some were not present on `List` and `Array` (e.g. `groupBy`). This leads to awkward code where `Seq`-producing functions are used even when `List` is an input. `Seq.groupBy` is a particular example.

Also, some functions were not defined on `Seq`, even though they exist in `List` or `Array`.

The proposal below is to complete the matrix for `List`, `Array` and `Seq` w.r.t. functional collection functions.

###Review and Completion

This work is only completed when all rows have a status of ":)". The library updates will only be done when all PRs are ready (or features are dropped).

#### Status column

- :) - reviewed and ready to be pulled
- :/ - reviewed but changes needed
- (empty) - no PR or not reviewed

If an item is marked "low-pri" it doesn't need to be completed in order for the library update to happen.

### Regular functional functions

Function | Comment | List | Array | Seq | PR | Status
-------- | ------- | ---- | ----- | --- | --- | ------
`append`|   | o | o | o | --- | n/a
`average`|   | o | o | o | --- | n/a
`averageBy`|   | o | o | o | --- | n/a
`contains` | new | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/e8ac90ab9f0aba2ce235c75e6327830577c6d95b)
`choose` |   | o | o | o | --- | n/a
`chunkBySize` |   | ADD | ADD | ADD | [PR](https://github.com/Microsoft/visualfsharp/pull/261) | [committed](https://github.com/Microsoft/visualfsharp/commit/a1a27a4d8884f093700ffd4f2843b7622a950199)
`collect` |   | o | o | o | --- | n/a
`compareWith` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/befea5b6c8182ba054831a6155a101df97e70c27)
`concat` |   | o | o | o | --- | n/a
`countBy` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/321dcde0fd686491d07d03f96d770d07a57f40cf)
`distinct` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/bebe9a2faecdaa340d4740b3ae639b506d0e2fef)
`distinctBy` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/bd6ac78c1378d5f565b86691f1e54f99e7339b4b)
`splitInto` |   | ADD | ADD | ADD | [PR](https://github.com/Microsoft/visualfsharp/pull/261) | [committed](https://github.com/Microsoft/visualfsharp/commit/a1a27a4d8884f093700ffd4f2843b7622a950199)
`empty` |   | o | o | o | --- | n/a
`exactlyOne` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/cc2834df2244bf973d9c60eb1977809f141ac621)
`except` |   | ADD | ADD | ADD | [PR](https://github.com/Microsoft/visualfsharp/pull/253) | [committed](https://github.com/Microsoft/visualfsharp/commit/0676493a20937884e0228a41dd0d29204a79577c)
`exists` |   | o | o | o | --- | n/a
`exists2` |   | o | o | o | --- | n/a
`filter` |   | o | o | o | --- | n/a
`find` |   | o | o | o | --- | n/a
`findBack` | new | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/4b52be7812360e6f562238d3c0e06576513dbeb9)
`findIndex` |   | o | o | o | --- | n/a
`findIndexBack` | new | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/4b52be7812360e6f562238d3c0e06576513dbeb9)
`fold` |   | o | o | o | --- | n/a
`fold2` |   | o | o | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/15f04de4e08ec8484938b3a853063cd0ab04e9fc)
`foldBack` |   | o | o | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/366777f04cb9a4387934911ea2e459a65a09ef53)
`foldBack2` |   | o | o | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/91c644d26c6a82e8d10d93f345be891738070c8c)
`forall` |   | o | o | o | --- | n/a
`forall2` |   | o | o | o | --- | n/a
`groupBy` |   | o | o | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/dc83c2c0d32f81731f87bd3b6970843c625ee9cb)
`head` |   | o | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/f197816d535ccc8608118b00e2cbb1c48d9c387f)
`indexed` | new, signature indexed: `C<T> -> C<int*T>` | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/9ceff4c86f830234348447a70577b5c6323ad9bb)
`init` |   | o | o | o | --- | n/a
`isEmpty` |   | o | o | o | --- | n/a
`item` | New, see note. Signature `int -> C<'T> -> 'T` | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/f028ee4a27b8e0d03b03d3e85caf6a94d8a350a9)
`iter` |   | o | o | o | --- | n/a
`iter2` |   | o | o | o | --- | n/a
`iteri` |   | o | o | o | --- | n/a
`iteri2` |   | o | o | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/d7bcad775cebd660f433c20acdebdd0a6428e635)
`last` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/70146cf9d6f225504553c342ca782724ea27f023)
`length` |   | o | o | o | --- | n/a
`map` |   | o | o | o | --- | n/a
`map2` |   | o | o | o | --- | n/a
`map3` |   | o | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/e36641b5c6fbf9b711d45170eba893afbc9d8721)
`mapi` |   | o | o | o | --- | n/a
`mapi2` |   | o | o | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/3a27cd8207bbc22be275ca54dec586b62d1c46d2)
`mapFold` | New, map + fold, with signature mapFold : `('State -> 'T -> 'U * 'State) -> 'State -> C<'T> -> C<'U> * 'State` e.g. see here | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/45d622c1239d7d0a0638346975f48267eb19f203)
`mapFoldBack` | New, map + fold, with signature mapFoldBack : `('T -> 'State -> 'U * 'State) -> C<'T> -> 'State -> C<'U> * 'State` | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/45d622c1239d7d0a0638346975f48267eb19f203)
`max` |   | o | o | o | --- | n/a
`maxBy` |   | o | o | o | --- | n/a
`min` |   | o | o | o | --- | n/a
`minBy` |   | o | o | o | --- | n/a
`nth` | see note | long-term deprecate, see note | o | long-term deprecate, see note | --- | n/a
`pairwise` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/b9541ad7030037d46d7846d37a32ed7903d2357e)
`permute` |   | o | o | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/a0f64602ced7ebcfe4970a69ba61e81f4dfeff8a)
`pick` |   | o | o | o | --- | n/a
`reduce` |   | o | o | o | --- | n/a
`reduceBack` |   | o | o | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/366777f04cb9a4387934911ea2e459a65a09ef53)
`replicate` |   | o | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/173d833660767fd24d523d09f317179cc3c3f4b9)
`rev` |   | o | o | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/da29df584ffe0c396a2fe5aee7d9c036bb5abc4e)
`scan` |   | o | o | o | --- | n/a
`scanBack` |   | o | o | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/df72269215be7f3a6d36fb4644e9428a80713baa)
`singleton` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/0e57aa3c3c6e174464a3b444c280ebb3151388d3)
`skip` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/627c016b8c7e57a875bc90f103468085632f286c)
`skipWhile` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/627c016b8c7e57a875bc90f103468085632f286c)
`sort` |   | o | o | o | --- | n/a
`sortBy` |   | o | o | o | --- | n/a
`sortWith` |   | o | o | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/6ae5c70b187243a6b655109a53ca87775aac79a7)
`sortDescending` |   | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/b446e2c4fb69edbd4714460568e767e89f54c8ca)
`sortByDescending` |   | ADD | ADD | ADD |  PR | [committed](https://github.com/Microsoft/visualfsharp/commit/b446e2c4fb69edbd4714460568e767e89f54c8ca)
`sum` |   | o | o | o | --- | n/a
`sumBy` |   | o | o | o | --- | n/a
`tail` |   | o | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/2d72eea64fab3417dbbd18cb5f465510c2847eb4)
`take` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/c80c8f079376d9498447faf2b35fc386c303dca0)
`takeWhile` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/c80c8f079376d9498447faf2b35fc386c303dca0)
`truncate` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/5f206976c42024100f0653be5d8a22584a569961)
`tryFind` |   | o | o | o | --- | n/a
`tryFindBack` | new | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/4b52be7812360e6f562238d3c0e06576513dbeb9)
`tryFindIndex` |   | o | o | o | --- | n/a
`tryFindIndexBack` | new | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/4b52be7812360e6f562238d3c0e06576513dbeb9)
`tryHead` | new | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/b10392dc16593bf9286583e2a87217d18fa18b8a)
`tryItem` | new | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/959cbdc81bfb08fc3210bfb92f4dfa0fb2504788)
`tryLast` | new | ADD | ADD | ADD | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/8407ad92b1ebfc3d237338fdad0d44f8be78b273)
`tryPick` |   | o | o | o | --- | n/a
`unfold` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/1d6fff28e0729ad787aacfa8e1789bd0150ca610)
`where` | syn. filter | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/0fd67dbc1e7d6721a5ec60b72d3d7c37637622fe)
`windowed` |   | ADD | ADD | o | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/b1485299763ecb14c6099a0006c42d1cc49ef414)
`zip` |   | o | o | o | --- | n/a
`zip3` |   | o | o | o | --- | n/a


Note: In F# 3.0 `Seq.where` was defined as a synonym for `Seq.filter`, mainly due to the use of "where" in query expressions. Given it already exists as a synonym (= decision made) it seems sensible to just complete the matrix and define `List.where` and `Array.where` as well.

Note: In F# 3.x, `nth` is defined with inconsistent signatures for `Array` and `List`. The proposal above replaces `nth` by `item` and would eventually deprecate `nth` (with a message to say 'please use Seq.item'. It also adds a corresponding `tryItem`. Both `item` and `tryItem` would take the integer index as the first parameter.

### Regular functional operators producing two or more output collections

These operators are not defined for `Seq.*` for performance reasons because using them would require iterating the input sequence twice.

Note it is arguable that if these are not defined, then `Seq.tail`, `Seq.skip` and `Seq.skipWhile` should also not be defined, since they implicitly skip inputs and can be a performance trap, especially when used recursively.

Function | Comment | List | Array | Seq | PR | Status
-------- | ------- | ---- | ----- | --- | --- | ------
`partition` |   | o | o | n/a | --- | n/a
`splitAt` | new, taking index | ADD | ADD | n/a | PR | [committed](https://github.com/Microsoft/visualfsharp/commit/1fc647986f79d20f58978b3980e2da5a1e9b8a7d)
`unzip` |   | o | o | n/a | --- | n/a
`unzip3` |   | o | o | n/a | --- | n/a

### Mutation-related operators

Function | Comment | List | Array | Seq | PR | Status
-------- | ------- | ---- | ----- | --- | --- | ------
`blit` |   | n/a | o | n/a | ---  | n/a 
`copy` |   | n/a | o | n/a | ---  | n/a 
`create` |   | n/a | o | n/a | ---  | n/a 
`fill` |   | n/a | o | n/a | ---  | n/a 
`get` |   | n/a | o | n/a | ---  | n/a 
`set` |   | n/a | o | n/a | ---  | n/a 
`sortInPlace` |   | n/a | o | n/a | ---  | n/a 
`sortInPlaceBy` |   | n/a | o | n/a | ---  | n/a 
`sortInPlaceWith` |   | n/a | o | n/a | ---  | n/a 
`sub` |   | n/a | o | n/a | ---  | n/a 
`zeroCreate` |   | n/a | o | n/a | ---  | n/a 

### Conversion operators

Function | Comment | List | Array | Seq | PR | Status
-------- | ------- | ---- | ----- | --- | --- | ------
`ofList` |   | n/a | o | o |  ---  | n/a 
`ofArray` |   | o | n/a | o | ---  | n/a 
`ofSeq` |   | o | o | n/a |  ---  | n/a 
`toList` |   | n/a | o | o |  ---  | n/a 
`toArray` |   | o | n/a | o | ---  | n/a 
`toSeq` |   | o | o | n/a | ---  | n/a 

### On-demand or IEnumerable computation operators

Function | Comment | List | Array | Seq | PR | Status
-------- | ------- | ---- | ----- | --- | --- | ------
`cache` |   | n/a | n/a | o | ---  | n/a 
`cast` |   | n/a | n/a | o |  ---  | n/a 
`delay` |   | n/a | n/a | o |  ---  | n/a 
`initInfinite` |   | n/a | n/a | o |  ---  | n/a 
`readonly` |   | n/a | n/a | o | ---  | n/a 
