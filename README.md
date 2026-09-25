# Developer Evaluation Project

`READ CAREFULLY`

## Instructions
**The test below will have up to 7 calendar days to be delivered from the date of receipt of this manual.**

- The code must be versioned in a public Github repository and a link must be sent for evaluation once completed
- Upload this template to your repository and start working from it
- Read the instructions carefully and make sure all requirements are being addressed
- The repository must provide instructions on how to configure, execute and test the project
- Documentation and overall organization will also be taken into consideration

## Use Case
**You are a developer on the DeveloperStore team. Now we need to implement the API prototypes.**

As we work with `DDD`, to reference entities from other domains, we use the `External Identities` pattern with denormalization of entity descriptions.

Therefore, you will write an API (complete CRUD) that handles sales records. The API needs to be able to inform:

* Sale number
* Date when the sale was made
* Customer
* Total sale amount
* Branch where the sale was made
* Products
* Quantities
* Unit prices
* Discounts
* Total amount for each item
* Cancelled/Not Cancelled

It's not mandatory, but it would be a differential to build code for publishing events of:
* SaleCreated
* SaleModified
* SaleCancelled
* ItemCancelled

If you write the code, **it's not required** to actually publish to any Message Broker. You can log a message in the application log or however you find most convenient.

### Business Rules

* Purchases above 4 identical items have a 10% discount
* Purchases between 10 and 20 identical items have a 20% discount
* It's not possible to sell above 20 identical items
* Purchases below 4 items cannot have a discount

These business rules define quantity-based discounting tiers and limitations:

1. Discount Tiers:
   - 4+ items: 10% discount
   - 10-20 items: 20% discount

2. Restrictions:
   - Maximum limit: 20 items per product
   - No discounts allowed for quantities below 4 items

## Overview
This section provides a high-level overview of the project and the various skills and competencies it aims to assess for developer candidates. 

See [Overview](/.doc/overview.md)

## Tech Stack
This section lists the key technologies used in the project, including the backend, testing, frontend, and database components. 

See [Tech Stack](/.doc/tech-stack.md)

## Frameworks
This section outlines the frameworks and libraries that are leveraged in the project to enhance development productivity and maintainability. 

See [Frameworks](/.doc/frameworks.md)

<!-- 
## API Structure
This section includes links to the detailed documentation for the different API resources:
- [API General](./docs/general-api.md)
- [Products API](/.doc/products-api.md)
- [Carts API](/.doc/carts-api.md)
- [Users API](/.doc/users-api.md)
- [Auth API](/.doc/auth-api.md)
-->

## Project Structure
This section describes the overall structure and organization of the project files and directories. 

See [Project Structure](/.doc/project-structure.md)

## Development setup

See [Development setup](/.doc/development-setup.md) for local secrets, database migrations, Docker Compose, and the optional development administrator.

The complete reviewer workflow, architecture, demonstration script, validation evidence, and known limitations are available in the [Setup and delivery guide](/.doc/setup-and-delivery.md).

## Run the complete application

Create `.env` from `.env.example`, provide the required local credentials, and start the complete stack:

```powershell
Copy-Item .env.example .env
docker compose up --detach --build --wait --wait-timeout 240
```

Open the frontend at `http://localhost:4200`. The API and Swagger are available at `http://localhost:5119` and `http://localhost:5119/swagger`.

## Frontend development and tests

Start the API first, then run the Angular development server:

```powershell
Set-Location src/frontend
npm ci
npm start
```

Run the frontend quality checks and unit tests from `src/frontend`:

```powershell
npm run lint
npx tsc -p tsconfig.app.json --noEmit
npm run test:ci -- --coverage --coverage-reporters=text-summary
npm run build
```

## End-to-end tests

The Playwright suite starts disposable API and PostgreSQL containers, generates credentials in memory, runs the Angular application, and removes its containers and volumes afterward. Docker must be running.

```powershell
Set-Location src/frontend
npm ci
npm run test:e2e:install
npm run test:e2e
```

See the [browser integration test guide](/src/frontend/e2e/README.md) for the covered scenarios and instructions for testing an existing environment.

## Postman collection

Import the following files into Postman:

- [DeveloperStore Sales API collection](/.doc/postman/DeveloperStore-Sales.postman_collection.json)
- [DeveloperStore local environment](/.doc/postman/DeveloperStore-Local.postman_environment.json)

Select the **DeveloperStore — Local** environment and set its secret `adminPassword` value to the development administrator password configured in `.env`. Run the collection in order. Its scripts authenticate automatically and capture the JWT, generated sale IDs, item IDs, and ETags required by later requests.

See the [Postman reviewer workflow](/.doc/postman/README.md) for folder descriptions, automatic variable behavior, cleanup details, and Newman execution.
