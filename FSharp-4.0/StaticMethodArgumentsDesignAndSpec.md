# F# 4.0 Speclet - Extending the F# type provider mechanism to allow methods to have static parameters

[User Voice](http://fslang.uservoice.com/forums/245727-f-language/suggestions/6097685-allow-static-arguments-to-type-provider-methods-e), [Pull Request](https://visualfsharp.codeplex.com/SourceControl/network/forks/dsyme/cleanup/contribution/7634), [Commit](https://github.com/dotnet/fsharp/commit/afe50271808ffa4f1b326c6ff029f45ec5ba0847)

### Background 

Integrating data, metadata (schema) and programming is a fundamental problem at the heart of much applied 
data programming and code-oriented data science. F# has an immensely powerful mechanism called F# type providers which allows for the scalable integration 
of data and schema. For example, you might want 

- CSV data manipulation where you can add/remove columns within a data script

- Embedding Regular Expression more Fluently in a programming language
- A Strongly Typed Data Frame Library

- Create/delete SQL tables in a strongly-typed, code-first way

- A search functionality in a massive Satori, Freebase or DbPedia knowledge graph (integrated into the programming language) which reveals individual entities

The feature implemented here will help enable these scenarios.


### Aim 

Add a fundamental extension to the F# type provider 
programming model that has particular uses when programs adjust or specify 
new schema during the course of execution.  

### Feature Design

From the point of view of the programmer, the feature lets them use type provider where methods can take static parameters:

    let x = ExampleType()

    let v1 = x.ExampleMethWithStaticParam<1>(1) 
    let v2 = x.ExampleMethWithStaticParam<2>(1,2) 
    let v3 = x.ExampleMethWithStaticParam<3>(1,2,3) 

Note that the sample method changes its number of static parameters with the static parameter given to the method. The return type can likewise be changed (and can be a provided type whose name depends on the input parameter)


From the point of view of the type provider author, using the modified ProvideTypes API, provided methods can now include a DefineStaticParameters specification:

    let staticParams = [ProvidedStaticParameter("Count", typeof<int>)]
    let exampleMethWithStaticParams =  
      let m = ProvidedMethod("ExampleMethWithStaticParam", [ ], typeof<int>, IsStaticMethod = false)
      m.DefineStaticParameters(staticParams, (fun nm args ->
          let arg = args.[0] :?> int
          let params = [ for i in 1 .. arg -> ProvidedParameter("arg" + string i, typeof<int>)]
          let m2 = 
              ProvidedMethod(nm, params, typeof<int>, IsStaticMethod = false,
                             InvokeCode = fun args -> <@@ arg @@>)
          newType.AddMember m2
          m2))
      m


The design is very much like that for ``DefineStaticParameters`` on types.

Notes:

- Here `nm` is the name to use for the actual instantiated method. It is a mangled name
- - The arguments and the return type on are irrelevant.  
- The staticness of  ``exampleMethWithStaticParams`` is relevant and must match the staticness of the instantiated method ``m2``. This is not checked.
- The method ``m2`` must be added to the same type as ``exampleMethWithStaticParams``


Underneath, the ITypeProvider API is extended by adding an ITypeProvider2 interface in FSharp.Core 4.4.0.0.  Additionally, the same methods can be defined using an idiom-pattern on a type provider, and we use reflection to interrogate the type provider object for the additional pair of methods if ITypeProvider2 is not implemented - for example if the type provider binds to FSharp.Core 4.3.1.0. See [the code in the commit](https://github.com/dotnet/fsharp/commit/afe50271808ffa4f1b326c6ff029f45ec5ba0847).

### Scenarios

#### CSV data manipulation where you can add/remove columns within a data script

A modified CSV type provider that lets you do add a column. The return type would be a _new_ object representing the data collection with the column added.  This is a bit like a "reccord calculus" where you can add and remove columns in user code in strongly typed ways (but can't write code that is generic over column types)


    type MyCsvFile = FSharp.Data.CsvProvider<"mycsv.csv">
    
    let csvData = MyCsvFile.LoadSample() // ....
       
    [ for row in csvData -> row.Column1, row.Column2 ]
       
    let newCsvData = csvData.WithColumn<"Column3", "int">()  
       
    [ for row in newCsvData -> row.Column1, row.Column2, row.Column3 ]
       
    let newCsvData2 = newCsvData.WithColumn<"Column4", "int">()  
       
    [ for row in newCsvData2 -> row.Column1, row.Column2, row.Column3, row.Column4 ]
       
    let newCsvData3 = newCsvData.RemoveColumn<"Column3", "int">()  
       
    [ for row in newCsvData2 -> row.Column1, row.Column2, row.Column4 ] // can't access Column3 anymore!

#### Embedding Regular Expression more Fluently

A regex type provider that lets you do this:

       RegEx.Parse<"a+b*c?">(data)
       
       RegEx.Match<"a+b*c?">(data)
       
       RegEx.IsMatch<"a+b*c?">(data)


#### A Strongly Typed Data Frame Library

A more strongly typed data frame library that lets you add/remove columns in a strongly typed functional way, like the CSV example.

#### Create/delete SQL tables in a strongly-typed, code-first way

A modified SqlClient type provider that takes the SQL command? - see [the original proposal](http://fslang.uservoice.com/forums/245727-f-language/suggestions/6097685-allow-static-arguments-to-type-provider-methods-e)


    type SqlConnect = SomeSQlProvider<"Some ConnectionString">

    let ctxt = SqlConnect.GetDataContext()
    
    let sqlTable = ctxt.CreateTable<"CREATE TABLE Foo COLUMNS X, Y">()  


#### A search functionality in the Freebase or DbPedia provider which reveals individual entities:

    type DbPedia = DbPediaProvider<"Some Parameters">

    let ctxt = DbPedia.GetDataContext()
    
    let princeTheMusician = ctxt.Ontology.People.Search<"Prince">. 

In the intellisense at the last point  the completions for all people matching "Prince" would be shown

### Design Alternatives - Can this be done today?

Sort of. Things generally get very nasty when you have a primary set of static parameters on a type, but some of the methods naturally take static parameters. With static parameters on provided methods you get to do something like this (at least, if things are working correctly)

      type DbPediaProvider<”A”>
                method Search<”B1”> (returns types depending on a search of DbPedia using string "B1")
                method Search<”B2”> (returns types depending on a search of DbPedia using string "B2")
                nested type SearchResults
                    nested type Search_B1_Results // use the mangled name provided for the method to create these types
                    nested type Search_B2_Results // use the mangled name provided for the method to create these types

Previously when you could only parameterize types doing this sort of thing was much more painful.



