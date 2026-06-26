# Unit Testing Principles

## 1. Arrange-Act-Assert

Structure every test in three clear phases: set up the inputs and preconditions (Arrange),
call the code under test (Act), then verify the result (Assert). This separation makes
tests easy to read and pinpoints exactly what is being verified.

## 2. Test Behaviour, Not Implementation

Write assertions against observable outputs and side effects, not internal details like
private method calls or intermediate state. Tests that are tied to implementation break
whenever you refactor, even when the behaviour stays correct.

## 3. Keep Tests Fast and Isolated

Each test should run independently of all others and complete in milliseconds. Avoid shared
mutable state and external resources (databases, network, filesystem); use fakes or
in-memory substitutes instead. Fast, isolated tests can run on every save and give instant
feedback.
