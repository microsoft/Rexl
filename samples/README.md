# Samples

This directory contains samples for Rexl and RexlScript. It comtains the directories
* [notebooks](#notebooks)
* [data](#data)

## Notebooks

The [notebooks](notebooks) directory contains
Jupyter notebook files. To run the notebooks, ensure that Jupyter is properly installed and that
the Rexl kernel is built and registered.

The Jupyter notebook files include:
* [NFL.ipynb](notebooks/NFL.ipynb): demonstrates
  some tabular data manipulation using NFL game data.
* [Comma.ipynb](notebooks/Comma.ipynb): discusses
  the _implicit lambda_ pattern and why Rexl doesn't need an analog of Excel's `SUMPRODUCT` function.

## Data

The [data](data) directory contains data files of various forms used in sample notebooks and scripts.
Some of the files are Rexl scripts that define one or more global symbols. Other files may be binary
data files (such as parquet or rbin files) or images (jpg, png) or just about anything.

The data files include:
* [NFL-2010-Games.rexl](data/NFL-2010-Games.rexl):
  A RexlScript file that defines a global named `Games` containing data for the NFL games played during the
  2010 regular season.
* [Orders.rexl](data/Orders.rexl): A RexlScript file
  that defines a global named `Orders` containing (fictitious) data for customer orders.

 **Note**: when adding a new kind of data file that should use LFS, ensure that the
 [`.gitattributes`](/.gitattributes) file has an entry for the extension.
