# Project Review

## Project Wide Comments
 - Overall, the codebase seems to return nulls and then throw an exception in services, instead of just throwing an exception in the first place. This can lead to confusion and potential null reference exceptions if not handled properly. It would be better to throw exceptions directly when an error occurs, to not lose information about what happened, and to avoid the `??` patterns in controllers.
 - Inconsistent response styling - Should assign the result of a service call to an appropriately named variable before returning it. also, response variables shouldnt be named "result" or "response". they should be named according to what they contain.
 - Only basic success/failure status codes are documented. Should document other possible status codes, even such as 404 for when the API is not up, 503 for when the service is temporarily unavailable, etc.
 - Dont bind and handle configuration like this. You should create a documented object that follows the shape of the configuration, and bind the configuration to an instance of that object. You can then inject these objects, syntax would look much cleaner in `Program.cs`, and the config will be clearly structured, typesafe and documented.
 - Api query parameters and routes (or basically anything used to communicate with the API that other services in this solution need) should be in a shared library , so that they can be reused and easily updated across the codebase. This also applies to any constants or enums that are used across the codebase. This is useful especially for bindings between the different services - changing query parameters in code in storage shouldnt necessarily change API, and changing API should be in one place and will uniformly affect everything.
 - Exceptions should generally extend the HTTP Exception to be able to have a status code, message and title associated with them aithout needing the error handler to do so. the error handler should only really need to worry about non-HTTP exceptions, and to log correct things (Info for 4XX and 2XX, Error for 5XX)
 - Generated swaggers should have their schemas documented, and not just plain types - currently, descriptions arent getting rendered from c# docstrings.
## Project Specific Comments

### Project: **`ClientManager.Api`**

#### Structure
 - Interfaces in `InternalClients/Interfaces`, `InternalClients/Interfaces/Configurations` are not documented, and should be.
 - No need for additional nesting of `/Configuration` after both `Services/InternalClients/Interfaces` and `Services/InternalClients/Implementations`
 - In `Services/InternalClients`, `StorageApi` classes should be moved to utils, under `/StorageApi`
 - Rename `/InternalClients` to `/Internal`

 ---

 ### Project: **`ClientManager.StorageApi`**

 #### Structure
  - Interfaces are missing documentation, and should be documented. At the same time, their implementers should either inherit the doc or have their own custom documentation.
  - Move `ClientLookup<T>` into the appropriate folder and file
  - Split `RuntimeModels`, `RuntimeMetrics`, `RuntimeExceptions`, `ConfigurationContractExceptions` into separate files
  - Split `RuntimeServices`, `StatisticsReadServices` into separate interface files
