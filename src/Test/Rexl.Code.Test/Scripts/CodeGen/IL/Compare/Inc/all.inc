// This file is included in comparison test files named "all.txt". It contains useful UDFs.

// Make tuples of all combinations from the given sequences.
`` func Glue(x, y) := ChainMap(a: x, ForEach(b: y, (a, b)));
`` func Glue(x, y, z) := ChainMap(a: x, ChainMap(b: y, ForEach(c: z, (a, b, c))));

// Format a sequence of tuples for output (to compare to baseline). Each tuple is expected to contain 5 bool values followed
// by k values that were compared. The bool values are the result of comparing using =, <, <=, >, >=, in that order.
`` func Polish(ts) := ForEach(t: ts, With(code: M(t[0]) & M(t[1]) & M(t[2]) & M(t[3]) & M(t[4]), (code, Op(code)) & t[5:]));

// Converts a bool value to "M" or "_". Polish concats these together to get a terse encoding of the results.
`` func M(x) := "M" if x else "_"; // Includers may override this (eg, to reverse it).

// Maps from the concatenated marks to a semantically meaningful representation of that pattern.
`` func Op(code) :=
``      "__" if code = "_____" else // Bad or not comparable.
``      "EQ" if code = "M_M_M" else
``      "LE" if code = "__M__" else
``      "LT" if code = "_MM__" else
``      "GE" if code = "____M" else
``      "GT" if code = "___MM" else
``      // Below happens with two operators.
``      "N___" if code = "MMMMM" else // Not, all bad.
``      "N_E_" if code = "_M_M_" else // Not, bad or eq.
``      "N_L_" if code = "M__MM" else // Not, bad or lt.
``      "N_G_" if code = "MMM__" else // Not, bad or gt.
``      "N_EL" if code = "___M_" else // Not, one eq and one lt.
``      "N_EG" if code = "_M___" else // Not, one eq and one gt.
``      "N_LG" if code = "M____" else // Not, one lt and one gt.
``      "***BAD!***"; // Unexpected pattern.
