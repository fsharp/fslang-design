
| Function   | Comment   | List      | Array     | Seq      |          |          |
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
| create	 	 |           |   ADD     |      o    |    ADD   |          |          |
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
| mapi2	 	 	 |           |           |           |          |          |          |
| max	 	 	   |           |           |           |          |          |          |
| maxBy	 	   |           |           |           |          |          |          |
| min	 	 	   |           |           |           |          |          |          |
| minBy	 	 	 |           |           |           |          |          |          |
| nth	 	 	   |           |           |           |          |          |          |
| ofArray	 	 |           |           |           |          |          |          |
| ofList	 	 |           |           |           |          |          |          |
| ofSeq	 	 	 |           |           |           |          |          |          |
| pairwise	 |           |           |           |          |          |          |
| partition	 |           |           |           |          |          |          |
| permute	 	 |           |           |           |          |          |          |
| pick	 	 	 |           |           |           |          |          |          |
| readonly	 |           |           |           |          |          |          |
| reduce	 	 |           |           |           |          |          |          |
| reduceBack |           |           |           |          |          |          |
| replicate	 | Similar to ‘copy’  |           |           |           |          |          |
| rev	 	 	 	 |           |           |           |          |          |          |
| scan	 	 	 |           |           |           |          |          |          |
| scanBack	 |           |           |           |          |          |          |
| set	 	 	 	 |           |           |           |          |          |          |
| singleton	 |           |           |           |          |          |          |
| skip	 	 	 |           |           |           |          |          |          |
| skipWhile	 |           |           |           |          |          |          |
| sort	 	 	 |           |           |           |          |          |          |
| sortBy	 	 |           |           |           |          |          |          |
| sortInPlace      |           |           |           |          |          |          |
| sortInPlaceBy	   |           |           |           |          |          |          |
| sortInPlaceWith	 |           |           |           |          |          |          |
| sorthWith	 	 	 	 |           |           |           |          |          |          |
| sub	 	 	 	   |           |           |           |          |          |          |
| sum	 	 	 	   |           |           |           |          |          |          |
| sumBy	 	 	 	 |           |           |           |          |          |          |
| tail	       | ‘tail’ usually indicates a list, but I’ve seen this called ‘rest’ in other languages, a dedicated version of “skip 1”	 	 	  |           |           |           |          |  
| take	 	 	 	 |           |           |           |          |          |          |
| takeWhile	 	 |           |           |           |          |          |          |
| toArray	     |           |           |           |          |          |          |
| toList	     |           |           |           |          |          |          |	 	 	         
| toSeq	 	 	   |           |           |           |          |          |          |
| truncate	 	 |           |           |           |          |          |          |
| tryFind	 	 	 |           |           |           |          |          |          |
| tryFindIndex |           |           |           |          |          |          |
| tryPick	 	 	 |           |           |           |          |          |          |
| unfold	 	 	 |           |           |           |          |          |          |
| unzip	 	 	 	 |           |           |           |          |          |          |
| unzip3	 	 	 |           |           |           |          |          |          |
| where	       | Same as ‘filter’	|           |          |          |          |          |
| windowed	 	 |           |           |           |          |          |          |
| zeroCreate	 |           |           |           |          |          |          |
| zip	 	 	 	   |           |           |           |          |          |          |
| zip3	 	 	 	 |           |           |           |          |          |          |
