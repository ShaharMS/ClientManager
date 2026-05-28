# Project Review

## Project Wide Comments
 - Overall, the codebase seems to return nulls and then throw an exception in services, instead of just throwing an exception in the first place. This can lead to confusion and potential null reference exceptions if not handled properly. It would be better to throw exceptions directly when an error occurs, to not lose information about what happened, and to avoid the `??` patterns in controllers.
 - Inconsistent response styling - Should assign the result of a service call to an appropriately named variable before returning it. also, response variables shouldnt be named "result" or "response". they should be named according to what they contain.
 - Only basic success/failure status codes are documented. Should document other possible status codes, even such as 404 for when the API is not up, 503 for when the service is temporarily unavailable, etc.
 - Dont bind and handle configuration like this. You should create a documented object that follows the shape of the configuration, and bind the configuration to an instance of that object. You can then inject these objects, syntax would look much cleaner in `Program.cs`, and the config will be clearly structured, typesafe and documented.
 - 

## Project Specific Comments

### Project: **`ClientManager.Api`**

#### Code
 - `StatisticsController:295` - helpers should not be placed in a controller file. in that specific case, i think that can be avoided entirely and be removed, using some type convertor like we do in enums.
 - `RuntimeStateClient:56,138,198,259` - Use fluent API: `StartStorageCall(...)\n.SetTag(...)\n.SetTag(...)`
 - `StatisticsReadClient:187`,`ServiceCatalogClient:63`,`ResourcePoolCatalogClient:63`,`GlobalRateLimitCatalogClient:66` - does this really need to be a function? why not just a parameter of type `Exception`?

#### Structure
 - Interfaces in `InternalClients/Interfaces`, `InternalClients/Interfaces/Configurations` are not documented, and should be.
 - No need for additional nesting of `/Configuration` after both `Services/InternalClients/Interfaces` and `Services/InternalClients/Implementations`
 - In `Services/InternalClients`, `StorageApi` classes should be moved to utils, under `/StorageApi`
 - Rename `/InternalClients` to `/Internal`

 ---

 ### Project: **`ClientManager.StorageApi`**

 #### Code


 #### Structure
  - Move `ClientLookup<T>` into the appropriate folder and file
  - Split `RuntimeModels`, `RuntimeMetrics`, `RuntimeExceptions`, `ConfigurationContractExceptions` into separate files
  - Split `RuntimeServices`, `StatisticsReadServices` into separate interface files
