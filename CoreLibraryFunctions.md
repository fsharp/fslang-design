
| Function   | Comment   | List      | Array     | Seq      |   xxxxxxxxxxxxxxxxxxx       |    xxxxxxxxxxxxxxx      |
|:-----------|:----------|:---------:|:---------:|:--------:|:--------:|:--------:|
| append	 	 |           |     o     |    o      |    o     |          |          |
| average	 	 |           |      o    |        o  |      o   |          |          |
| averageBy	 |           |    o      |      o    |    o     |          |          |
| blit	 	 	 |           |     n/a   |   o       |   n/a    |          |          |
| cache	 	 	 |           |    n/a    |    n/a    |   o      |          |          |
| cast	 	 	 |           |   n/a     |   n/a     |   o      |          |          |
| choose	 	 |           |   o       |     o     |   o      |          |          |
| collect	 	 |           |  o        |      o    |      o   |          |          |
| compareWith|           |  ADD      |     ADD   |     o    |          |          |
| concat	 	 |           |     o     |       o   |     o    |          |          |
| copy	 	 	 |           |   n/a     |     o     |     n/a  |          |          |
| countBy	 	 |           |  ADD      |     ADD   |      o   |          |          |
| create	 	 |           |   n/a     |      o    |    n/a   |          |          |
| delay	 	 	 |           |    n/a    |    n/a    |    o     |          |          |
| distinct	 |           |   ADD     |     ADD   |     o    |          |          |
| distinctBy |           |    ADD    |    ADD    |    o     |          |          |
| empty	 	 	 |           |    o      |    o      |      o   |          |          |
| exactlyOne |           |    ADD    |    ADD    |        o |          |          |
| exists	 	 |           |     o     |       o   |     o    |          |          |
| exists2	 	 |           |    o      |        o  |      o   |          |          |
| fill	 	 	 |           |   n/a     |     o     |     n/a  |          |          |
| filter	 	 |           |   o       |     o     |     o    |          |          |
| find	 	 	 |           |   o       |     o     |     o    |          |          |
| findIndex	 |           |  o        |      o    |      o   |          |          |
| fold	 	 	 |           |     o     |     o     |     o    |          |          |
| fold2	 	 	 |           |   o       |    o      |     ADD  |          |          |
| foldBack	 |           |   o       |    o      |   ADD    |          |          |
| foldBack2	 |           |   o       |   o       |    ADD   |          |          |
| forall	 	 |           |   o       |  o        |     o    |          |          |
| forall2	 	 |           |  o        |   o       |      o   |          |          |
| get	       | “get i” same as “nth (i + 1)” ?	 	 	 |           |     o      |           |          |          |
| groupBy	 	 |           |    o      |       o   |    ADD   |          |          |
| head	 	 	 |           |   o       |    ADD    |   o      |          |          |
| init	 	 	 |           |   o       |    o      |     o    |          |          |
| initInfinite |         |    n/a    |   n/a     |    o     |          |          |
| isEmpty	 	 |           |    o      |     o     |      o   |          |          |
| iter	 	 	 |           |   o       |      o    |     o    |          |          |
| iter2	 	 	 |           |    o      |       o   |    o     |          |          |
| iteri	 	 	 |           |    o      |       o   |    o     |          |          |
| iteri2	 	 |           |   o       |      o    |   ADD    |          |          |
| last	 	 	 |           |   ADD     |    ADD    |     o    |          |          |
| length	 	 |           |   o       |    o      |     o    |          |          |
| map	 	 	   |           |    o      |     o     |      o   |          |          |
| map2	 	 	 |           |   o       |    o      |     o    |          |          |
| map3	 	 	 |           |   o       |    ADD    |   ADD    |          |          |
| mapi	 	 	 |           |   o       |    o      |     o    |          |          |
| mapi2	 	 	 |           |  o        |   o        |    ADD      |          |          |
| max	 	 	   |           |    o      | o          |  o        |          |          |
| maxBy	 	   |           |    o      | o          |    o      |          |          |
| min	 	 	   |           |  o        |         o  |  o        |          |          |
| minBy	 	 	 |           |    o       |   o        |    o      |          |          |
| nth	 	 	   |           |      o     | ADD          |  o        |          |          |
| ofArray	 	 |           |      o     | n/a          |    o      |          |          |
| ofList	 	 |           |   n/a        |    o       |   o       |          |          |
| ofSeq	 	 	 |           |      o     |       o    |    n/a      |          |          |
| pairwise	 |           |     ADD      |    ADD       |     o     |          |          |
| partition	 |           |    o       |       o    |    n/a      |          |          |
| permute	 	 |           |    o       |       o    |      n/a    |          |          |
| pick	 	 	 |           |     o      |        o   |     o     |          |          |
| readonly	 |           |     n/a      |      n/a     |   o       |          |          |
| reduce	 	 |           |     o      |        o   |     o     |          |          |
| reduceBack |           |    o       |         o  |      ADD    |          |          |
| replicate	 | Similar to ‘create’  |     o      |    ADD       |   ADD        |          |          |
| rev	 	 	 	 |           |    o       |   o        |    ADD      |          |          |
| scan	 	 	 |           |     o      |      o     |     o     |          |          |
| scanBack	 |           |     o      |    o       |   ADD       |          |          |
| set	 	 	 	 |           |    n/a     |   o        |    n/a      |          |          |
| singleton	 |           |    ADD    |     ADD      |    o      |          |          |
| skip	 	 	 |           |   ADD        |      ADD     |   o       |          |          |
| skipWhile	 |           |  ADD         |     ADD      |    o      |          |          |
| sort	 	 	 |           | o          |    o       |     o     |          |          |
| sortBy	 	 |           |   o        |      o     |     o     |          |          |
| sortInPlace  |         |    n/a     |           |          |          |          |
| sortInPlaceBy  |       |           |           |          |          |          |
| sortInPlaceWith	 |     |           |           |          |          |          |
| sorthWith	 	 	 	 |     |           |           |          |          |          |
| sub	 	 	 	 |           |           |           |          |          |          |
| sum	 	 	 	 |           |           |           |          |          |          |
| sumBy	 	 	 |           |           |           |          |          |          |
| tail	     | ‘tail’ usually indicates a list, but I’ve seen this called ‘rest’ in other languages, a dedicated version of “skip 1”	 	 	  |           |           |           |          |  
| take	 	 	 |           |           |           |          |          |          |
| takeWhile	 |           |           |           |          |          |          |
| toArray	   |           |           |           |          |          |          |
| toList	   |           |           |           |          |          |          |	 	 	         
| toSeq	 	 	 |           |           |           |          |          |          |
| truncate	 |           |           |           |          |          |          |
| tryFind	 	 |           |           |           |          |          |          |
| tryFindIndex |         |           |           |          |          |          |
| tryPick	 	 |           |           |           |          |          |          |
| unfold	 	 |           |           |           |          |          |          |
| unzip	 	 	 |           |           |           |          |          |          |
| unzip3	 	 |           |           |           |          |          |          |
| where	     | Same as ‘filter’	|           |          |          |          |          |
| windowed	 |           |           |           |          |          |          |
| zeroCreate |           |           |           |          |          |          |
| zip	 	 	 	 |           |           |           |          |          |          |
| zip3	 	 	 |           |           |           |          |          |          |
