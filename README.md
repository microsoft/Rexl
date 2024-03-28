# Rexl

Rexl, also written RExL or REXL, stands for Research Expression Language.
It shares heritage and some design goals with [Microsoft Power Fx](https://github.com/microsoft/Power-Fx).
It is designed to be embedded in applications.

This repository contains:
* The core Rexl functionality including lexer, parser, binder, and code generator.
* Flow graph functionality, with data nodes and formula nodes, where dependencies (edges) are defined by Rexl formulas.
* [ONNX runtime](https://github.com/microsoft/onnxruntime) integration with some sample models taken from the
  [ONNX Model Zoo](https://github.com/onnx/models). The models are exposed as functions in the Rexl language.
* Solver integration, including a Boolean Satisfiability Solver (SAT solver), and several linear MIP solvers.
* Statement Rexl, also known as RexlScript.
* Harness functionality for embedding RexlScript in an application.
* Some sample applications, including:
    * A Jupyter Notebook kernel usable with both [Jupyter Lab/Notebook](https://jupyter.org/).
    * RexlBench, a GUI application for editing and running RexlScript.
    * RexlRun, a command line application for running RexlScript.
    * DocBench, a GUI application for editing and evaluating flow graphs.
* Sample notebooks for Jupyter and sample scripts for RexlBench/RexlRun.
* Documentation.

## Contents

* [Overview](#overview)
* [Building](#building)
* [Packages](#packages)
* [Samples](#samples)
* [Contributing](#contributing)
* [Trademarks](#trademarks)

## Overview

Core Rexl is a pure functional expression language. It consists of the following projects:
* [Rexl.Base](https://github.com/microsoft/Rexl/tree/main/src/Core/Rexl.Base): type system, name handling and
  other utilities.
* [Rexl.Bind](https://github.com/microsoft/Rexl/tree/main/src/Core/Rexl.Bind): Contains the lexer, parser,
  and binder, as well as parse tree and semantic (bound) tree representations.
* [Rexl.Code](https://github.com/microsoft/Rexl/tree/main/src/Core/Rexl.Code): Contains MSIL/CIL code
  generation and type manager functionality. A type manager is responsible for mapping from Rexl type
  (known as `DType`) to .Net `System.Type`.

Other projects include:
* [Rexl.Flow](https://github.com/microsoft/Rexl/tree/main/src/Core/Rexl.Code): Contains the flow graph
  functionality.
* [Rexl.Onnx](https://github.com/microsoft/Rexl/tree/main/src/Core/Rexl.Onnx): Contains basic
  [ONNX runtime](https://github.com/microsoft/onnxruntime) integration including sample functions wrapping
  models from the [ONNX Model Zoo](https://github.com/onnx/models).
* [Rexl.Solve](https://github.com/microsoft/Rexl/tree/main/src/Core/Rexl.Solve): Contains support for
  invoking a SAT solver and several linear MIP solvers.
* [Rexl.Harness](https://github.com/microsoft/Rexl/tree/main/src/Core/Rexl.Harness): Contains harness
  functionality for executing RexlScript. This includes support for RexlScript's task concept.

## Building

The [Rexl.sln](https://github.com/microsoft/Rexl/tree/main/src) file can be built in the standard ways
using Microsoft Visual Studio or the `dotnet` command line application. For example, from a command
prompt:
* `cd` to the `src` directory.
* Run `dotnet build Rexl.sln -c Debug` to build the debug configuration.
* Run `dotnet build Rexl.sln -c Release` to build the release configuration.

### Running Tests

Similarly the tests can be run in the standard ways using Microsoft Visual Studio or the `dotnet`
command line application:
* `cd` to the `src` directory.
* Run `dotnet test Rexl.sln -c Debug` to run the tests for the debug configuration.
* Run `dotnet test Rexl.sln -c Release` to run the tests for the release configuration.

### Running Jupyter

To run RexlScript in a Jupyter notebook:
* Build `Debug` or `Release`.
* `cd` to the `src\Apps\Kernel` directory.
* Register the Jupyter kernel by running `RegisterKernel.cmd Debug` or `RegisterKernel.cmd Release`.
* `cd` to the directory where you want to create notebooks.
* Run `jupyter lab` or `jupyter notebook`.

### Running Sample Applications

To run the other sample applications, `RexlBench`, `RexlRun`, or `DocBench` simply run the corresponding
`.exe` from with Visual Studio, from the command line, or from windows explorer.

## Packages

We will be publishing nuget packages soon.

## Samples

We will be adding samples soon. Until then, the test scripts contain many examples of Rexl and RexlScript.
For example, see [`src/Test/Rexl.Code.Test/Scripts/Block/General/Basic.txt`](https://github.com/microsoft/Rexl/blob/main/src/Test/Rexl.Code.Test/Scripts/Block/General/Basic.txt) for some sample RexlScript.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
