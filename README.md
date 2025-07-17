# Sample Ordering Api

This sample project demonstrates the implementation of an Azure Functions-based API for processing customer orders. In simple terms, this API receives order details, validates input, manages inventory, processes payments, and sends email notifications. For testing purposes, it simulates payment processing and email notifications.

## How to Run

### Prerequisites

- .NET 8 SDK
- Azure Functions Core Tools v4
- VS Code with Azure Functions extension OR Visual Studio 2022
- [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension in VS Code to test the API directly from the editor (optional)

### Steps

1. Clone this repo to your local machine
2. Open in VS Code or Visual Studio 2022
3. Run `dotnet build`
4. Press F5 to start debugging OR run `func start` in terminal
5. Test using HTTP client or Postman. Alternatively, you can use REST Client extension. Sample request can be found in the `.http` folder at the root level.
