# MLCalc User's Manual

> **Disclaimer:** MLCalc is a personal project, provided "as is" and without any warranty. Like
> all software, it may contain bugs, so its results can be wrong. Do not use MLCalc where an
> incorrect result is not acceptable, for example in medical, safety-critical, engineering,
> financial or legal work. If a result matters, check it with another method or tool. The full
> terms are in the [MIT license](../LICENSE).

MLCalc is a calculator and plotting program with its own small programming language. It works
with numbers, complex numbers and matrices, solves equations, fits polynomials, integrates and
differentiates functions, and draws graphs. It began as an Amiga program in the 1990s; this version
runs on .NET as a Windows desktop application and as a cross-platform command-line tool.

## Contents

1. [Getting started](#1-getting-started)
2. [Numbers and values](#2-numbers-and-values)
3. [Variables](#3-variables)
4. [Matrices](#4-matrices)
5. [Operators](#5-operators)
6. [Statements and control flow](#6-statements-and-control-flow)
7. [Function reference](#7-function-reference)
8. [Plotting](#8-plotting)
9. [Console commands and scripts](#9-console-commands-and-scripts)
10. [The desktop application](#10-the-desktop-application)
11. [The command-line tool](#11-the-command-line-tool)
12. [Errors](#12-errors)
13. [Differences from MATLAB and from the original MLCalc](#13-differences-from-matlab-and-from-the-original-mlcalc)
14. [Limitations](#14-limitations)
- [Appendix A: All functions and commands](#appendix-a-all-functions-and-commands)
- [Appendix B: Keywords, operators and constants](#appendix-b-keywords-operators-and-constants)

In the examples, `>>` is the prompt: the text after it is what you type, and the lines below are
what MLCalc answers.

---

## 1. Getting started

### The desktop application

Start **MLCalc**. The window has three parts:

- the **plot area** at the top, where graphs are drawn;
- the **console** below it, with the results and an input line at the bottom marked `>>`;
- the **status bar**, which shows whether a program is running and the coordinates under the mouse.

Type an expression in the input line and press **Enter**. Section
[10](#10-the-desktop-application) describes the application in detail.

### The command-line tool

Run `mlcalc` in a terminal to get the same `>>` prompt. Type `exit` to quit. `mlcalc script.m`
runs a script and `mlcalc -e "expression"` evaluates an expression. Section
[11](#11-the-command-line-tool) has all the options.

### A first session

```
>> 1 + 2
ans = 3
>> x = sqrt(2) * 3
x = 4.242640687
>> A = [1 2; 3 4]
A =
  1  2
  3  4
>> A * [1; 1]
ans =
  3
  7
>> z = (1 + 2i)^2
z = -3+4i
>> solve("x", "x^2 - 2", 1)
ans = 1.414213562
```

And a graph:

```
xrange(-6.3, 6.3); yrange(-6, 6); grid
fplot("x", "x*sin(x)")
```

---

## 2. Numbers and values

### Every value is a complex matrix

MLCalc has one kind of value: a matrix of complex numbers.

- A **number** such as `3` is a 1×1 matrix.
- A **real number** is a complex number whose imaginary part is zero.
- An **empty matrix** `[]` has no elements.

Results stay real whenever the mathematically correct answer is real, and become complex only
when needed:

```
>> sqrt(16)
ans = 4
>> sqrt(-4)
ans = 2i
>> ln(-1)
ans = 3.141592654i
```

### Writing numbers

| Form | Examples |
|---|---|
| Integer | `42` |
| Decimal | `3.25`  `.5`  `1.` |
| Exponent | `1e-3`  `2.5E+2` (= 250) |
| Imaginary | `2i`  `2j`  `0.5i` |
| Complex | `3 + 4i`  `-1.5 - 2i` |

The decimal separator is always a point. A number may not be followed directly by a letter: `3x`
is an error; write `3*x`.

### Constants

| Name | Value |
|---|---|
| `pi` | π = 3.141592654… |
| `i`, `j` | the imaginary unit, √−1 |
| `eps` | 2.220446049e-16, the spacing between 1 and the next larger number |
| `inf` | positive infinity, shown as `Inf` |
| `nan` | "not a number", shown as `NaN` |
| `ans` | the most recent result (see [ans](#ans)) |

Names are case-insensitive, so `PI`, `Pi` and `pi` are the same. A variable with the same name as
a constant hides the constant: after `i = 5`, `i` is 5. The literal `1i` always means the imaginary
unit.

### How results are shown

- Numbers are shown with up to 10 significant digits: `1/3` gives `0.3333333333`.
- Complex numbers are shown as `a+bi`: `0.5-0.5i`, `2i`.
- Infinity is `Inf` or `-Inf`; undefined results are `NaN`.
- Matrices are shown with their columns right-aligned. Matrices with more than 10 000 elements
  are summarised instead; show a part of them with indexing, e.g. `A(1:5, 1:5)`.

```
>> 1/3
ans = 0.3333333333
>> z = 1/(1 + i)
z = 0.5-0.5i
>> 1e300 * 1e10
ans = Inf
>> [1.5 -2i]
ans =
  1.5  -2i
```

Numbers that are too large become `Inf`, but **division by zero is an error**, not `Inf`, and so is
raising 0 to a negative power (`0^-1`). `0^0` is 1.

---

## 3. Variables

### Assignment

`name = expression` stores a value. Names start with a letter or `_` and continue with letters,
digits and `_`. Letters from any language are allowed (`längd = 3`).

```
>> radius = 2
radius = 2
>> area = pi * radius^2
area = 12.56637061
```

- **Names are case-insensitive**: `Radius` and `radius` are the same variable.
- **A `;` at the end hides the result**: `x = 5;` stores the value without showing it.
- A variable may have the same name as a function or a constant; the variable then takes
  precedence until it is deleted with `del`.

### ans

When you type an expression that is not an assignment, the result is stored in the variable `ans`:

```
>> 6 * 7
ans = 42
>> ans + 1
ans = 43
```

- Assignments do not change `ans`.
- Commands such as `plot` or `xrange` do not change `ans`.
- Typing just the name of a variable shows it without changing `ans`.
- A large result keeps its memory while it is in `ans`, until the next result replaces it;
  `del(ans)` frees it at once. The same holds for any large variable: `del(name)`.

### Listing and deleting variables

`list` (or `who`) lists all variables with their sizes; `del(name)` deletes a variable, and
`del(all)` deletes all of them. See [Console commands](#9-console-commands-and-scripts).

---

## 4. Matrices

### Writing matrices

Write a matrix in square brackets:

- Separate the **elements of a row** with commas or spaces.
- Separate **rows** with semicolons or new lines.

```
>> A = [1, 2, 3; 4, 5, 6]
A =
  1  2  3
  4  5  6
>> B = [1 2
... 3 4]
B =
  1  2
  3  4
```

Rows must all have the same length. Matrices can be put together from smaller matrices, as long as
the sizes fit:

```
>> v = [1 2];
>> [v, 3; 4 5 6]
ans =
  1  2  3
  4  5  6
```

`[]` is the empty matrix. Empty parts are ignored when matrices are joined.

**Spaces and signs.** Inside brackets, a `+` or `-` with a space before it but not after it starts
a new element; otherwise it is an operator:

```
>> [1 -2]
ans =
  1  -2
>> [1 - 2]
ans = -1
```

Similarly `[f (1)]` is two elements (`f` and `1`), while `[f(1)]` is a call to `f`.

### Ranges

`start:end` creates a row with the numbers from `start` to `end` in steps of 1, and
`start:step:end` uses the given step:

```
>> 1:5
ans =
  1  2  3  4  5
>> t = 0:0.25:1
t =
  0  0.25  0.5  0.75  1
>> 3:-1:1
ans =
  3  2  1
```

If the step goes the wrong way (as in `3:1`) the result is empty. The step must not be zero.

### Indexing

Indices start at 1. `A(r, c)` is the element in row `r` and column `c`.

```
>> A = [1 2 3; 4 5 6];
>> A(2, 3)
ans = 6
```

| Form | Meaning |
|---|---|
| `A(r, c)` | one element |
| `A(r, :)` | all of row `r` |
| `A(:, c)` | all of column `c` |
| `A(rows, cols)` | a sub-matrix; `rows` and `cols` can be vectors or ranges |
| `A(k)` | the k-th element, counting down the columns (column by column) |
| `A(:)` | all elements as one column |

```
>> A = [1 2 3; 4 5 6];
>> A(:, 2:3)
ans =
  2  3
  5  6
>> A(2)
ans = 4
>> v = [10 20 30];
>> v([3 1])
ans =
  30  10
```

Indexing a row vector with a vector gives a row; indexing a column vector gives a column.
Indices must be positive whole numbers that are not larger than the size.

### Changing elements

Assigning to an indexed element changes it. If the index is outside the matrix, the matrix
**grows** and the new elements are set to zero:

```
>> x = [10 20 30];
>> x(5) = 50
x =
  10  20  30  0  50
>> M(2, 3) = 7
M =
  0  0  0
  0  0  7
```

- With two indices, any matrix can grow, and the variable does not need to exist beforehand.
- With one index, only vectors (and empty or new variables) can grow.
- The value assigned must be a single number (it is copied to every selected element) or have
  as many elements as are selected: `A(2, :) = [7 8 9]`.

---

## 5. Operators

### Arithmetic

MLCalc follows MATLAB: the plain operators work on whole matrices, and the operators with a dot
work element by element.

| Operator | Meaning |
|---|---|
| `a + b`, `a - b` | addition and subtraction, element by element |
| `a * b` | matrix product; if either side is a single number, every element is multiplied |
| `a / b` | right division: `a * inv(b)`; if `b` is a single number, every element is divided |
| `a \ b` | left division: solves `a * x = b` for `x` |
| `a ^ b` | power: for numbers the ordinary power; for a square matrix a whole-number power (`A^2 = A*A`, `A^-1 = inv(A)`) |
| `a .* b` | element-by-element product |
| `a ./ b` | element-by-element division |
| `a .\ b` | element-by-element left division (`b ./ a`) |
| `a .^ b` | element-by-element power |
| `a mod b` | remainder after division (real numbers) |
| `a div b` | whole-number division, rounded toward zero (real numbers) |
| `-a`, `+a` | negation, no change |

**Single numbers combine with anything.** When an element-by-element operation combines a matrix
with a single number, the number is used with every element: `[1 2 3] + 10` is `[11 12 13]`.
Otherwise both sides must have the same size.

```
>> A = [1 2; 3 4];
>> A * A
ans =
   7  10
  15  22
>> A .* A
ans =
  1   4
  9  16
>> A \ [5; 11]
ans =
  1
  2
>> 7 mod 3
ans = 1
>> -7 mod 3
ans = -1
>> 7 div 2
ans = 3
```

`mod` keeps the sign of the number being divided, so `-7 mod 3` is `-1` (MATLAB's `mod` would give
`2`). `mod` and `div` can also be written as functions: `mod(7, 3)`, `div(7, 2)`.

### Comparison

| Operator | Meaning |
|---|---|
| `==` | equal |
| `~=` or `<>` | not equal |
| `<`, `<=`, `>`, `>=` | less than, at most, greater than, at least |

Comparisons work element by element and give 1 (true) or 0 (false). `==` and `~=` compare both
the real and imaginary parts; the ordering comparisons compare only the real parts.

```
>> [1 5 3] > 2
ans =
  0  1  1
```

### Logical operators

| Operator | Meaning |
|---|---|
| `and`, `&`, `&&` | true if both are non-zero |
| `or`, `\|`, `\|\|` | true if either is non-zero |
| `xor` | true if exactly one is non-zero |
| `not`, `~` | true if zero |

Any non-zero number counts as true. The results are 1 or 0, element by element. Both sides are
always evaluated (`&&` and `||` do not skip the right-hand side).

### Postfix operators

| Operator | Meaning |
|---|---|
| `a'` | conjugate transpose: rows become columns and complex numbers are conjugated |
| `a.'` | transpose without conjugating |
| `n!` | factorial, element by element |

```
>> [1 2; 3 4]'
ans =
  1  3
  2  4
>> (1 + 2i)'
ans = 1-2i
>> 5!
ans = 120
```

For whole numbers `n!` is 1·2·…·n. For other non-negative numbers it is the gamma function
Γ(n + 1), so `0.5!` is √π/2. Negative or complex arguments are errors.

### Precedence

Operators higher in the table bind tighter:

| Precedence | Operators |
|---|---|
| highest | postfix `'` `.'` `!` |
| | `^` `.^` |
| | unary `-` `+` `not` `~` |
| | `*` `/` `\` `.*` `./` `.\` `mod` `div` |
| | `+` `-` |
| | `:` (ranges) |
| | comparisons `==` `~=` `<>` `<` `<=` `>` `>=` |
| | `and` `&` `&&` |
| lowest | `or` `xor` `\|` `\|\|` |

Operators on the same level are evaluated from left to right. Use parentheses to change the order.

```
>> -2^2
ans = -4
>> 2^-1
ans = 0.5
>> 2^3^2
ans = 64
```

`-2^2` is `-(2^2)`, as in mathematics and MATLAB. An exponent may have its own sign (`2^-1`).
Powers are evaluated from left to right: `2^3^2` is `(2^3)^2` = 64.

---

## 6. Statements and control flow

### Statements and separators

A program is a sequence of statements. A statement is ended by:

| Separator | Effect |
|---|---|
| new line | ends the statement; the result is shown |
| `,` | ends the statement; the result is shown |
| `;` | ends the statement; the result is **not** shown |

So several statements can share a line: `a = 1; b = 2, c = 3` shows `b` and `c`.

`%` starts a **comment** that runs to the end of the line. `...` at the end of a line
**continues** the statement on the next line. Inside parentheses, a statement may continue on the
next line without `...`.

```
total = 1 + 2 + ...     % continues on the next line
	3 + 4;
y = max(total,          % inside parentheses, no ... needed
	10)
```

### Conditions

`if` and `while` test a condition. A condition is **true** if it is non-empty and all its elements
are non-zero. So `[1 1 0]` is false and `[]` is false.

### Two styles

Every control statement can be written in two styles. MLCalc picks the style from what follows the
condition:

- **MATLAB style**: the condition ends the line (or is followed by `,` or `;`), and the statements
  that follow run up to a closing `end`.
- **Original MLCalc style**: the condition is followed by `then` or `do`, and the body is a single
  statement or a `begin … end` block.

Both styles can be mixed in the same program.

### if

MATLAB style:

```
if x > 0
	sign = 1;
elseif x < 0
	sign = -1;
else
	sign = 0;
end
```

The `elseif` and `else` parts are optional; one `end` closes the whole statement. On one line:
`if x > 0, y = 1, else, y = 2, end`.

Original style:

```
if x > 0 then y = 1 else y = 2

if a == 0 then
begin
	x = 1;
	y = 2
end
else if a == 1 then x = 3
else x = 4
```

In the original style, `else` may be on a later line. It belongs to the nearest `if … then`
before it.

### while

```
k = 0;
while k < 3
	k = k + 1;
end
```

Original style: `while k < 3 do k = k + 1;` or with a `begin … end` block. On one line in MATLAB
style: `while k < 3, k = k + 1; end`.

The loop runs as long as the condition is true. In the desktop application and the command-line
tool a running loop can be stopped with **Esc** or **Ctrl+C**. The same keys stop a single long
calculation, such as multiplying or inverting a large matrix.

### for

```
s = 0;
for k = 1:10
	s = s + k;
end
```

| Form | Values of `k` |
|---|---|
| `for k = a to b` | `a`, `a+1`, `a+2`, … up to `b` (none if `a > b`) |
| `for k = 1:2:9` | each element of the range: 1, 3, 5, 7, 9 |
| `for k = M` | each column of the matrix `M` in turn |

Add `do` after the header for the original style (`for k = 1 to 10 do s = s + k;`); without `do`
the body runs to `end`. After the loop, `k` keeps its last value.

### begin … end

`begin` and `end` group statements into one, for use as the body of an original-style `if`,
`while` or `for`:

```
while k < 3 do
begin
	k = k + 1;
	disp(k)
end
```

### Entering several lines

When a statement is not finished at the end of a line — an `if` or loop without its `end`, an
unclosed bracket or parenthesis — the console shows the prompt `...` and waits for the rest. An
empty line runs what has been typed so far (and reports what is missing). In the desktop
application, **Esc** discards the unfinished input.

---

## 7. Function reference

In this chapter, "element by element" means that the function is applied to every element of a
matrix argument, and the result has the same size. Unless stated otherwise, functions accept
complex arguments and work in radians.

Functions are called with parentheses: `sin(x)`. Functions without arguments, such as `rand`, can
also be called without parentheses.

### 7.1 Trigonometric functions

All element by element.

| Function | Meaning |
|---|---|
| `sin(x)`, `cos(x)`, `tan(x)` | sine, cosine, tangent |
| `cot(x)` | cotangent, cos/sin |
| `asin(x)`, `acos(x)`, `atan(x)` | inverse functions (principal values); `asin(2)` is complex |
| `acot(x)` | inverse cotangent, `atan(1/x)`; `acot(0)` = π/2 |

### 7.2 Hyperbolic functions

All element by element.

| Function | Meaning |
|---|---|
| `sinh(x)`, `cosh(x)`, `tanh(x)`, `coth(x)` | hyperbolic sine, cosine, tangent, cotangent |
| `asinh(x)`, `acosh(x)`, `atanh(x)` | inverse functions; `acosh(x)` for x < 1 and `atanh(x)` for \|x\| > 1 are complex |
| `acoth(x)` | inverse hyperbolic cotangent, `atanh(1/x)` |

### 7.3 Exponentials and logarithms

All element by element.

| Function | Meaning |
|---|---|
| `exp(x)` | eˣ |
| `ln(x)`, `log(x)` | natural logarithm; for negative or complex `x` the principal complex value; `ln(0)` is an error |
| `log10(x)` | base-10 logarithm |

### 7.4 Powers and roots

All element by element.

| Function | Meaning |
|---|---|
| `sqrt(x)` | square root; `sqrt(-4)` is `2i` |
| `sqr(x)` | square, x² |
| `xroot(x, n)` | the n-th root; for negative `x` and odd `n` the real root: `xroot(-8, 3)` is `-2` |

Note that `(-8)^(1/3)` gives the complex principal root, while `xroot(-8, 3)` gives `-2`.

### 7.5 Complex numbers

All element by element.

| Function | Meaning |
|---|---|
| `abs(z)` | absolute value (modulus) |
| `arg(z)`, `angle(z)` | argument (angle) in radians, between −π and π; `arg(0)` is 0 |
| `re(z)`, `real(z)` | real part |
| `im(z)`, `imag(z)` | imaginary part |
| `conj(z)` | complex conjugate |
| `sign(z)` | −1, 0 or 1 for real numbers; `z/abs(z)` for complex numbers |

```
>> abs(3 + 4i)
ans = 5
>> sign(3 + 4i)
ans = 0.6+0.8i
```

### 7.6 Rounding and remainders

All element by element. The rounding functions round the real and imaginary parts separately.

| Function | Meaning |
|---|---|
| `round(x)` | nearest whole number; halves are rounded away from zero (`round(2.5)` is 3, `round(-2.5)` is −3) |
| `ceil(x)` | round up |
| `floor(x)` | round down |
| `fix(x)` | round toward zero |
| `mod(a, b)`, `rem(a, b)` | remainder of a/b, with the sign of `a` (same as the operator `a mod b`); real numbers only |
| `div(a, b)` | a/b rounded toward zero (same as `a div b`); real numbers only |

### 7.7 Combinatorics

| Function | Meaning |
|---|---|
| `factorial(n)` | n!, element by element (same as the operator `n!`) |
| `comb(n, k)` | number of combinations, "n choose k" |
| `perm(n, k)` | number of permutations of k items out of n |

`comb` and `perm` need non-negative whole numbers and give 0 when `k > n`. They work element by
element; a single number is combined with every element of the other argument.

```
>> comb(5, 2)
ans = 10
>> perm(5, 2)
ans = 20
```

### 7.8 Creating matrices

| Function | Result |
|---|---|
| `ones(n)`, `ones(r, c)`, `ones(A)` | a matrix of ones |
| `zeros(n)`, `zeros(r, c)`, `zeros(A)` | a matrix of zeros (also spelled `zeroes`) |
| `id(n)`, `id(r, c)`, `id(A)` | identity matrix: ones on the diagonal (also `eye`) |
| `rand`, `rand(n)`, `rand(r, c)`, `rand(A)` | random numbers between 0 and 1 |

With one number `n` the result is n×n; with two numbers it is r×c; with a matrix `A` it has the
same size as `A`. `rand` without arguments gives a single random number.

```
>> ones(2, 3)
ans =
  1  1  1
  1  1  1
>> id(2)
ans =
  1  0
  0  1
```

### 7.9 Size

| Function | Result |
|---|---|
| `size(A)` | `[rows columns]` |
| `row(A)`, `rows(A)` | number of rows |
| `col(A)`, `columns(A)` | number of columns |
| `length(A)` | the larger of the two dimensions (0 for an empty matrix) |
| `numel(A)` | number of elements |

```
>> size([1 2 3; 4 5 6])
ans =
  2  3
```

### 7.10 Linear algebra

| Function | Result |
|---|---|
| `det(A)` | determinant of a square matrix |
| `inv(A)` | inverse of a square matrix |
| `transpose(A)` | transpose without conjugation (same as `A.'`) |
| `gauss(A, B)` | solves `A * X = B` by Gaussian elimination (same as `A \ B`) |

The operators `A \ B` and `A / B` also solve linear systems (see [Arithmetic](#arithmetic)).
Systems are solved by Gaussian elimination with pivoting; `B` may have several columns, each one a
right-hand side. A singular matrix gives the error *The equation system has no unique solution*.

```
>> det([1 2; 3 4])
ans = -2
>> inv([1 2; 3 4])
ans =
   -2     1
  1.5  -0.5
```

### 7.11 Sums, products, maximum and minimum

| Function | Result |
|---|---|
| `sum(A)` | sum of the elements of a vector; for a matrix, the sum of each column (a row) |
| `prod(A)` | product, in the same way |
| `max(A)`, `min(A)` | largest/smallest element of a vector; for a matrix, of each column |
| `max(A, B)`, `min(A, B)` | element by element, the larger/smaller of `A` and `B` |

`max` and `min` compare real parts and ignore `NaN` values (the result is `NaN` only when all
values are `NaN`).

```
>> sum([1 2; 3 4])
ans =
  4  6
>> max([1 5; 7 2])
ans =
  7  5
>> max([1 4], [3 2])
ans =
  3  4
```

### 7.12 Polynomials

Polynomials are vectors of coefficients in **ascending** order of power: `[c0 c1 c2]` means
c0 + c1·x + c2·x².

| Function | Result |
|---|---|
| `polyval(p, x)` | the polynomial `p` evaluated at every element of `x` |
| `polyfit(x, y, n)` | the polynomial of degree `n` that best fits the points (x, y) in the least-squares sense; needs more than `n` points with at least `n + 1` different x values |
| `polyplot(p)` | draws the polynomial (see [Plotting](#8-plotting)) |

```
>> p = polyfit([0 1 2 3], [1 3 4 4], 1)
p =
  1.5  1
>> polyval(p, [0 10])
ans =
  1.5  11.5
```

The fitted line is 1.5 + x.

### 7.13 Functions given as text

Some functions work on a whole function of one variable, such as "x² − 2". The function is given
as two strings in double quotes: the **name of the variable** and an **expression**:

```
solve("x", "x^2 - 2", 1)
```

- The expression may use any functions and the current variables: with `a = 3`,
  `integral("t", "a*t", 0, 1)` is 1.5.
- The variable named in the first string is local to the function: evaluating it does **not**
  change a variable of the same name.
- Commands such as `plot`, `xrange`, `del`, `cls` or `savefig` cannot be used in a function given
  as text, because it is evaluated many times; the function is refused before it runs at all.
- The expression must give a single number for each value of the variable. For the numerical
  methods below, its real part is used.
- Parts of the expression that do not depend on the variable, such as `sin(pi/4)*a` in
  `"sin(pi/4)*a*x^2"`, are computed only once, which makes plots and numerical methods faster.
  `rand` is always evaluated again.

| Function | Result |
|---|---|
| `feval(var, expr, x)` | the function evaluated at every element of `x` |
| `integral(var, expr, a, b)` | the definite integral from `a` to `b` |
| `derivata(var, expr, x0)`, `derivative(…)` | the derivative at `x0` |
| `solve(var, expr, x0)` | a root (where the function is 0) near `x0` |
| `isect(var, f, g, x0)` | where the functions `f` and `g` are equal, near `x0` |
| `extreme(var, expr, x0)` | a local maximum or minimum near `x0` (where the derivative is 0) |
| `fplot(var, expr)`, `tplot(…)` | draws the function (see [Plotting](#8-plotting)) |

```
>> feval("t", "t^2", [0 1 2])
ans =
  0  1  4
>> integral("x", "x^2", 0, 1)
ans = 0.3333333333
>> derivata("x", "x^3", 2)
ans = 12
>> solve("x", "x^2 - 2", 1)
ans = 1.414213562
>> isect("x", "x^2", "x + 2", 1.5)
ans = 2
>> extreme("x", "(x - 1.5)^2 + 4", 0)
ans = 1.5
```

**How they work:**

- `integral` uses Simpson's rule, halving the step until two successive estimates agree to about
  12 significant digits. A function that is undefined somewhere in the interval gives an error,
  and so does an integral that does not settle (for example of `sin(1/x)` close to 0).
- `derivata` uses central differences with repeated extrapolation (Ridders' method), starting from
  several step sizes, accurate to about 10 digits for smooth functions, also ones that vary quickly
  such as `sin(1000*x)`. Where no reliable value can be found, for example at a jump such as
  `sign(x)` at 0, the result is an error.
- `solve` starts at `x0` and searches outward in both directions, first in steps of 0.01 and then
  with slowly growing steps, until the function changes sign. The root is then refined with
  Brent's method to full precision. It therefore finds the root closest to `x0`. Sign changes
  caused by poles (such as tan at π/2) are skipped. Roots where the function touches zero
  without changing sign (such as x² at 0) cannot be found. If nothing is found the error is
  *No root found near x0*.
- `isect` solves `f − g = 0` in the same way.
- `extreme` solves `derivative = 0` in the same way; it does not tell whether it found a maximum or
  a minimum.

---

## 8. Plotting

### 8.1 Plot windows

The plot area starts with one window whose axes run from −10 to 10. It can be split into a grid of
up to 3×3 windows; all plot commands draw in the **current window**.

| Command | Effect |
|---|---|
| `initdisplay(r, c)` | splits the plot area into `r` rows and `c` columns of new, empty windows (1 to 3 each) and makes the top-left window current |
| `currentwin(r, c)` | makes the window in row `r`, column `c` current |

When there is more than one window, the current one is outlined in blue.

```
initdisplay(1, 2)
fplot("x", "sin(x)")
currentwin(1, 2)
fplot("x", "cos(x)")
```

### 8.2 Axes, titles and grid

| Command | Effect |
|---|---|
| `xrange(a, b)` | sets the x axis to run from `a` to `b` (`a < b`) and **clears the window** |
| `yrange(a, b)` | sets the y axis to run from `a` to `b` and **clears the window** |
| `grid`, `grid(1)` | shows grid lines; `grid(0)` hides them |
| `title("text")` | title above the window |
| `xtitle("text")`, `xlabel("text")` | label under the x axis |
| `ytitle("text")`, `ylabel("text")` | label beside the y axis |
| `clear`, `clf` | removes everything drawn in the current window and resets the pen to 1 |

Because `xrange` and `yrange` clear the window, set the ranges **before** drawing. The axes through
the origin are drawn automatically when 0 is inside the range. Note that `clear` clears the plot,
not the variables; use `del(all)` for that.

### 8.3 Pens and colours

Each window has a current **pen**, numbered 1 to 15, each with its own colour. Higher numbers wrap
around (pen 16 is pen 1).

- `pen(n)` sets the pen of the current window.
- `plot`, `cplot`, `fplot`, `tplot` and `polyplot` draw with the current pen and then move to the
  next one, so consecutive curves get different colours.
- `dot` draws with the current pen without changing it.
- `clear`, `xrange` and `yrange` reset the pen to 1; call `pen` after them.

| Pen | Colour | Pen | Colour | Pen | Colour |
|---|---|---|---|---|---|
| 1 | blue | 6 | brown | 11 | dark blue |
| 2 | red | 7 | pink | 12 | dark red |
| 3 | green | 8 | cyan | 13 | olive green |
| 4 | orange | 9 | olive | 14 | dark brown |
| 5 | purple | 10 | grey | 15 | plum |

### 8.4 Drawing

| Command | Draws |
|---|---|
| `plot(y)` | the values of `y` against 1, 2, 3, … |
| `plot(x, y)` | the points (x, y) joined by lines |
| `plot(x, y, style)` | the points with the given style |
| `cplot(z)`, `cplot(z, style)` | complex numbers as points in the complex plane |
| `fplot(var, expr)`, `tplot(var, expr)` | the graph of a function given as text |
| `polyplot(p)` | the graph of the polynomial with coefficients `p` |
| `dot(x, y)` | a single dot at (x, y) |

**Styles** are written as a bare symbol or as a string:

| Style | Appearance |
|---|---|
| `-` | points joined by lines (the default) |
| `+` | a cross at each point |
| `*` | a star at each point |
| `"."` or `!` | a dot at each point |

```
x = 0:0.5:6;
plot(x, sin(x), +)
plot(x, cos(x))
```

**plot.** `x` must be a vector. If `y` is a vector it must have the same number of elements. If
`y` is a matrix, each row with as many elements as `x` (or else each column) is drawn as a
separate curve, each with the next pen. Only the real parts are used. A non-finite value
(`NaN`, `Inf`) breaks the line.

**cplot.** Each element is drawn as the point (real part, imaginary part). The window becomes a
complex-plane window with axes labelled Re and Im. Switching a window between `cplot` and the other
plot commands clears it.

```
xrange(-1.5, 1.5); yrange(-1.5, 1.5)
cplot(exp(i * (0:0.2:6.4)), +)
```

**fplot and tplot** (the same command under two names) evaluate the expression at 1000 points
across the current x range and join them. Points where the function is undefined, such as
`1/x` at 0, are left out, and the curve is broken at poles, such as tan at π/2, rather than
joined by a vertical line. If the result is complex, the real part is drawn.
The variable named in the first string does not affect your variables. Errors in the expression
itself, such as an index outside a matrix or matrices of different sizes, are reported instead of
leaving the plot empty.

```
xrange(-6.3, 6.3); yrange(-6, 6)
fplot("x", "tan(x)")
```

**polyplot** draws a polynomial over the current x range in the same way; the coefficients are in
ascending order, as for `polyval`.

**dot** is meant for drawing many single points, typically in a loop:

```
yrange(0, 1); xrange(2.5, 4)
for r = 2.5:0.005:4
	x = 0.5;
	for k = 1:200
		x = r * x * (1 - x);
		if k > 150, dot(r, x), end
	end
end
```

### 8.5 Saving plots

| Command | Effect |
|---|---|
| `savefig("file.svg")` | saves the plot area as an SVG image, 800×600 |
| `savefig("file.svg", w, h)` | the same with width `w` and height `h` (50 to 16000) |
| `savefig("file.png")`, `savefig("file.png", w, h)` | saves a PNG image (**desktop application only**) |

A relative file name is relative to the folder of the running script, or else to the working
folder (see [Scripts](#scripts)). All windows of the grid are saved in one image.

Other ways to save plots:

- In the desktop application, **File › Export Plot** and **Edit › Copy Plot** (see
  [section 10](#10-the-desktop-application)).
- In the command-line tool, the option `--svg file.svg` (see [section 11](#11-the-command-line-tool)).

`print` is kept from the original MLCalc but only shows a message; use `savefig` instead.

---

## 9. Console commands and scripts

### Console commands

| Command | Effect |
|---|---|
| `list`, `who` | lists the variables with their sizes, marking complex ones |
| `del(x)`, `del(x, y, …)` | deletes the named variables; the names can also be strings: `del("x")` |
| `del(all)` | deletes all variables |
| `cls`, `clc` | clears the console |
| `disp(x)` | shows a value without a name |
| `disp("text")` | shows a text |
| `run("file.m")` | runs a script |

```
>> x = [1 2]; z = 1 + i;
>> del(x, q)
x is deleted.
q is not a variable.
>> disp("hello")
hello
>> disp([1 2 3])
  1  2  3
```

### Scripts

A script is a text file with the extension `.m` containing MLCalc statements. Run it by:

- typing its file name at the prompt, e.g. `FEIGENBA.m`;
- `run("path/to/file.m")` at the prompt or from another script;
- **File › Run Script** or drag and drop in the desktop application;
- `mlcalc file.m` on the command line.

While a script runs, relative file names (in `run` and `savefig`) are relative to the **script's
folder**. Otherwise they are relative to the working folder: the `samples` folder of the
desktop application, or the current folder of the command-line tool.

A script shares the variables of the console: it can use variables you have defined, and the
variables it creates remain afterwards.

Execution stops at the first error. The message gives the line and column in the script; for a
nested `run`, the message names the inner script: `In bad.m at 2:5: …`. If the script has a syntax
error, none of it is run.

The original MLCalc sample scripts are included in the `samples` folder, for example
`FEIGENBA.m` (a Feigenbaum diagram), `FYSIK.m` (measurement data), `TEST5.m` (polynomial fitting)
and `COS.m` (several plot windows).

---

## 10. The desktop application

### Input

| Key | Effect |
|---|---|
| **Enter** | runs the input (or continues it on the next line if it is unfinished) |
| **Up**, **Down** | earlier and later input |
| **Esc** | stops a running program; when nothing is running, discards unfinished input |

### Output

The console shows your input in blue, results in black, errors in red and notes in grey. Select
lines with the mouse (Shift and Ctrl select several) and press **Ctrl+C** to copy them; with
nothing selected, **Edit › Copy Output** copies the whole console.

The console keeps the latest 10 000 lines, and fewer when they are long (about 16 million
characters in all, which is around a hundred printed 100×100 matrices). A program can print far
faster than a window can show text, so when output arrives too fast, some lines are skipped and a
grey note says how many; the program itself is not affected. End statements in loops with `;` to avoid printing them.

### Menus and shortcuts

| Menu item | Shortcut | Effect |
|---|---|---|
| File › Run Script… | Ctrl+O | chooses and runs a `.m` file |
| File › Export Plot… | Ctrl+E | saves the plot area as PNG (twice the screen resolution) or SVG |
| File › Exit | | closes the application |
| Edit › Copy Output | Ctrl+C | copies the selected console lines (or all lines) |
| Edit › Copy Plot | Ctrl+Shift+C | copies the plot area to the clipboard as an image |
| Edit › Stop Script | Esc | stops the running program |
| View › Clear Console | Ctrl+L | clears the console |
| View › Reset Plots | | returns to a single, empty plot window |
| Help › List Functions | | lists all functions, commands and constants |

### Other features

- The **status bar** shows "Running…" while a program runs, and the coordinates under the mouse
  pointer in the plot area (with the window number when there are several windows).
- **Drag a `.m` file** onto the window to run it.
- The divider between the plot area and the console can be dragged.
- The plot is redrawn when the window is resized; long-running scripts update the plot as they run.
- **Starting with a script:** `MLCalc --run script.m` (or `MLCalc script.m`) runs the script when
  the application starts.

---

## 11. The command-line tool

```
mlcalc [options] [script.m ...]
```

| Option | Effect |
|---|---|
| `-e code`, `--eval code` | evaluates `code`; may be repeated; runs after the scripts |
| `-i`, `--interactive` | starts the prompt after running the scripts and expressions |
| `--svg file` | saves the plots to an SVG file when done |
| `--size WxH` | size of the SVG image, e.g. `1200x800` (default `800x600`) |
| `-h`, `--help` | shows help |
| `--version` | shows the version |

**What runs:**

1. The scripts, in the order given.
2. The `-e` expressions, in order.
3. If there were no scripts or expressions and input is piped in, the piped text runs as a script:
   `echo "inv([1 2; 3 4])" | mlcalc`.
4. The interactive prompt, if `-i` was given or nothing else was.
5. Finally, the plots are saved if `--svg` was given (also after an error, showing what was drawn).

Execution stops at the first error. Errors are written to the error stream as
`source:line:column: message`, where the source is the script name, `-e` or `stdin`.

**Exit codes:** 0 on success, 1 when a program failed, 2 for invalid options.

**At the prompt:** `help` shows a summary, `exit` or `quit` leaves, and **Ctrl+C** stops a running
program (or leaves when nothing is running; pressing it a second time before the program has
stopped also leaves). The first time a plot command is used, a note reminds
you that plots can be saved with `savefig`. Unfinished input continues with the `...` prompt, as in
the desktop application.

**Examples:**

```
mlcalc FEIGENBA.m --svg feigenbaum.svg --size 1200x800
mlcalc -e "A = [1 2; 3 4]" -e "inv(A)"
mlcalc analysis.m -i
```

*Windows PowerShell 5.1* removes inner double quotes from arguments, so
`mlcalc -e 'fplot("x","sin(x)")'` fails there. Use PowerShell 7, cmd, bash, a script file or piped
input instead.

---

## 12. Errors

When something goes wrong, MLCalc shows a message with the position of the problem as
`line:column`, for example:

```
>> x = [1 2] * [3 4]
1:5: Cannot multiply a 1×2 matrix by a 1×2 matrix.
```

- A **syntax error** (a statement MLCalc cannot read) means that nothing of the input is run.
- A **runtime error** stops the program at the statement where it happens; statements before it
  have already run.

Common messages:

| Message | Cause |
|---|---|
| *Expected an expression* | something is missing, as in `x = 1 +` |
| *Expected 'then' or the end of the line, but found …* | an `if`/`while`/`for` header continues with something unexpected |
| *Unknown function or variable 'q'* | a misspelt or undefined name |
| *Matrix dimensions do not agree: 1×2 and 1×3* | an element-by-element operation on matrices of different sizes |
| *Cannot multiply a 1×2 matrix by a 1×2 matrix* | matrix product with incompatible sizes; perhaps `.*` was meant |
| *Division by zero* | division by zero, or `mod`/`div` by zero |
| *Index 3 exceeds the size 2* | reading outside a matrix |
| *Index must be a positive integer, got 0* | indices start at 1 |
| *Cannot assign a 1×2 matrix to 1×3 elements* | the value assigned does not fit the selected elements |
| *The equation system has no unique solution (the matrix is singular)* | `inv`, `\`, `/` or `gauss` on a singular matrix |
| *A matrix can only be raised to an integer power* | `A^0.5` for a matrix; perhaps `.^` was meant |
| *sin: expected 1 argument, got 2* | wrong number of arguments |
| *fplot: argument 1 must be a string in double quotes* | the variable and the expression must be strings |
| *In "y + x": Unknown function or variable 'y'* | an error inside a function given as text |
| *'del' is a command and cannot be used in a function given as text* | commands are not allowed in `fplot`, `feval`, `solve` and similar functions |
| *No root found near 0* | `solve` found no sign change |
| *A 46341×46341 matrix would have 2147488281 elements; the limit is 25000000* | a matrix (or range) larger than the size limit, see [Limitations](#14-limitations) |
| *The program is nested too deeply (more than 256 levels …)* | too many nested parentheses, brackets, blocks, or operators in one chain |
| *Scripts are nested too deeply (does a script run itself?)* | scripts running each other more than 32 levels deep |
| *The plot window is full: it can hold at most 2000000 points* | too many dots or plots in one window; clear it first, see [Limitations](#14-limitations) |
| *Internal error (…): …* | an unexpected problem in MLCalc itself; the program stops, but MLCalc keeps working |
| *Strings can only be used as arguments to functions such as fplot or title* | strings are not values |

---

## 13. Differences from MATLAB and from the original MLCalc

### Compared with MATLAB

MLCalc's operators and indexing follow MATLAB, but it is a much smaller language:

- Strings are written in double quotes and can only be given to functions; there are no string
  variables. `'` is always the transpose operator.
- `end` cannot be used inside an index (`x(end)`).
- At most two indices: no three-dimensional arrays.
- There are no user-defined functions, `break`, `continue` or `return`; scripts are the unit of
  reuse.
- Functions of a variable are given as text: `fplot("x", "sin(x)")` rather than `@(x) sin(x)`.
- `mod` keeps the sign of the dividend (like MATLAB's `rem`).
- `&&` and `||` evaluate both sides.
- `clear` clears the current plot, not the variables (use `del(all)`).
- Names are case-insensitive.
- In addition to MATLAB-style blocks, the original `then`/`do`/`begin … end` forms are accepted, and
  so are `<>` (not equal), `!` (factorial), `mod` and `div` as operators.

### Compared with the original Amiga MLCalc

- **Operators follow MATLAB.** In the original, `*` and `/` worked element by element and `.*`
  was the matrix product; now `*` is the matrix product and `.*` works element by element. Scripts
  that multiply matrices of equal size with `*` must use `.*` instead. None of the included
  sample scripts are affected.
- `-2^2` is −4 (the original computed (−2)² = 4).
- `row(A)` gives the number of rows (the original returned the number of columns).
- New: MATLAB-style blocks, `for` loops, ranges `a:b`, `elseif`, growing matrices by assignment,
  `size`, `length`, `sum`, `prod`, `max`, `min`, `inv`, `disp`, `extreme`, `run`, `savefig` and more.
- More accurate numerical methods (Brent's method for `solve`, Ridders' method for `derivata`),
  and several corrections to the complex inverse functions.
- `print` no longer prints; use `savefig`.
- The logarithmic and polar plot window types of the original are not available.

---

## 14. Limitations

- Matrix powers (`A^p`) only for whole-number `p`; use `.^` for element-by-element powers.
- At most two indices; `end` is not allowed in indices.
- No user-defined functions, `break`, `continue` or string variables.
- Up to 3×3 plot windows; no logarithmic or polar axes and no legends.
- `savefig` to PNG only in the desktop application; the command-line tool saves SVG.

To protect the computer running MLCalc, a few sizes are limited. Exceeding a limit gives an error
message; nothing is changed.

| Limit | Value |
|---|---|
| Elements in one matrix or range | 25 000 000 (for example 5000×5000); each element takes 16 bytes. `polyfit` keeps (degree + 1) values per point and has the same limit for them |
| Nesting of parentheses, brackets, blocks and operator chains | 256 levels (a sum of up to about 250 terms) |
| Scripts running other scripts | 32 levels |
| Elements shown in the console | 10 000; larger results are summarised as, for example, `A = [200×200 matrix, too large to show; …]` |
| Points in one plot window | 2 000 000 (dots, plotted points and curves together); `clear`, `xrange` or `yrange` empties the window |
| Lines kept in the desktop console | the latest 10 000, and at most about 16 million characters |
| `savefig` image size | 50 to 16000 |

---

## Appendix A: All functions and commands

| Name | Description | Section |
|---|---|---|
| `abs` | absolute value | [7.5](#75-complex-numbers) |
| `acos`, `acot` | inverse cosine, cotangent | [7.1](#71-trigonometric-functions) |
| `acosh`, `acoth` | inverse hyperbolic cosine, cotangent | [7.2](#72-hyperbolic-functions) |
| `angle`, `arg` | argument of a complex number | [7.5](#75-complex-numbers) |
| `asin`, `atan` | inverse sine, tangent | [7.1](#71-trigonometric-functions) |
| `asinh`, `atanh` | inverse hyperbolic sine, tangent | [7.2](#72-hyperbolic-functions) |
| `ceil` | round up | [7.6](#76-rounding-and-remainders) |
| `clc`, `cls` | clear the console | [9](#console-commands) |
| `clear`, `clf` | clear the current plot window | [8.2](#82-axes-titles-and-grid) |
| `col`, `columns` | number of columns | [7.9](#79-size) |
| `comb` | combinations | [7.7](#77-combinatorics) |
| `conj` | complex conjugate | [7.5](#75-complex-numbers) |
| `cos`, `cot` | cosine, cotangent | [7.1](#71-trigonometric-functions) |
| `cosh`, `coth` | hyperbolic cosine, cotangent | [7.2](#72-hyperbolic-functions) |
| `cplot` | plot in the complex plane | [8.4](#84-drawing) |
| `currentwin` | select the current plot window | [8.1](#81-plot-windows) |
| `del` | delete variables | [9](#console-commands) |
| `derivata`, `derivative` | derivative | [7.13](#713-functions-given-as-text) |
| `det` | determinant | [7.10](#710-linear-algebra) |
| `disp` | show a value or text | [9](#console-commands) |
| `div` | whole-number division | [7.6](#76-rounding-and-remainders) |
| `dot` | draw a dot | [8.4](#84-drawing) |
| `exp` | exponential | [7.3](#73-exponentials-and-logarithms) |
| `extreme` | local maximum or minimum | [7.13](#713-functions-given-as-text) |
| `eye`, `id` | identity matrix | [7.8](#78-creating-matrices) |
| `factorial` | factorial | [7.7](#77-combinatorics) |
| `feval` | evaluate a function given as text | [7.13](#713-functions-given-as-text) |
| `fix` | round toward zero | [7.6](#76-rounding-and-remainders) |
| `floor` | round down | [7.6](#76-rounding-and-remainders) |
| `fplot`, `tplot` | plot a function given as text | [8.4](#84-drawing) |
| `gauss` | solve a linear system | [7.10](#710-linear-algebra) |
| `grid` | show or hide grid lines | [8.2](#82-axes-titles-and-grid) |
| `im`, `imag` | imaginary part | [7.5](#75-complex-numbers) |
| `initdisplay` | split the plot area into windows | [8.1](#81-plot-windows) |
| `integral` | definite integral | [7.13](#713-functions-given-as-text) |
| `inv` | matrix inverse | [7.10](#710-linear-algebra) |
| `isect` | intersection of two functions | [7.13](#713-functions-given-as-text) |
| `length` | largest dimension | [7.9](#79-size) |
| `list`, `who` | list variables | [9](#console-commands) |
| `ln`, `log` | natural logarithm | [7.3](#73-exponentials-and-logarithms) |
| `log10` | base-10 logarithm | [7.3](#73-exponentials-and-logarithms) |
| `max`, `min` | largest, smallest | [7.11](#711-sums-products-maximum-and-minimum) |
| `mod`, `rem` | remainder | [7.6](#76-rounding-and-remainders) |
| `numel` | number of elements | [7.9](#79-size) |
| `ones` | matrix of ones | [7.8](#78-creating-matrices) |
| `pen` | set the pen | [8.3](#83-pens-and-colours) |
| `perm` | permutations | [7.7](#77-combinatorics) |
| `plot` | plot points and lines | [8.4](#84-drawing) |
| `polyfit` | least-squares polynomial | [7.12](#712-polynomials) |
| `polyplot` | plot a polynomial | [8.4](#84-drawing) |
| `polyval` | evaluate a polynomial | [7.12](#712-polynomials) |
| `print` | not supported; use `savefig` | [8.5](#85-saving-plots) |
| `prod` | product | [7.11](#711-sums-products-maximum-and-minimum) |
| `rand` | random numbers | [7.8](#78-creating-matrices) |
| `re`, `real` | real part | [7.5](#75-complex-numbers) |
| `round` | round to nearest | [7.6](#76-rounding-and-remainders) |
| `row`, `rows` | number of rows | [7.9](#79-size) |
| `run` | run a script | [9](#scripts) |
| `savefig` | save the plots as an image | [8.5](#85-saving-plots) |
| `sign` | sign | [7.5](#75-complex-numbers) |
| `sin` | sine | [7.1](#71-trigonometric-functions) |
| `sinh`, `tanh` | hyperbolic sine, tangent | [7.2](#72-hyperbolic-functions) |
| `size` | rows and columns | [7.9](#79-size) |
| `solve` | root of a function | [7.13](#713-functions-given-as-text) |
| `sqr` | square | [7.4](#74-powers-and-roots) |
| `sqrt` | square root | [7.4](#74-powers-and-roots) |
| `sum` | sum | [7.11](#711-sums-products-maximum-and-minimum) |
| `tan` | tangent | [7.1](#71-trigonometric-functions) |
| `title` | window title | [8.2](#82-axes-titles-and-grid) |
| `transpose` | transpose | [7.10](#710-linear-algebra) |
| `xrange`, `yrange` | axis ranges | [8.2](#82-axes-titles-and-grid) |
| `xroot` | n-th root | [7.4](#74-powers-and-roots) |
| `xtitle`, `xlabel`, `ytitle`, `ylabel` | axis labels | [8.2](#82-axes-titles-and-grid) |
| `zeros`, `zeroes` | matrix of zeros | [7.8](#78-creating-matrices) |

## Appendix B: Keywords, operators and constants

**Keywords** (case-insensitive, cannot be used as names):
`and begin div do else elseif end for if mod not or then to while xor`

**Operators:** `+ - * / \ ^ .* ./ .\ .^ ' .' ! = == ~= <> < <= > >= & && | || ~ :`

**Constants:** `pi i j eps inf nan`, and the variable `ans`.

The complete syntax is described in EBNF in [grammar.md](grammar.md).
