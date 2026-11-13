# MLCalc grammar

This document describes the language accepted by `MLCalc.Language` — the lexer
([`Lexer.cs`](../src/MLCalc.Language/Lexer.cs)) and the recursive-descent parser
([`Parser.cs`](../src/MLCalc.Language/Parser.cs)). The EBNF follows the structure of the parser:
each nonterminal corresponds to a parse method, and the order of the expression rules gives
operator precedence.

A few rules depend on context (whitespace, lookahead, bracket nesting) and cannot be expressed in
plain EBNF. They are marked with ⚑ in the grammar and explained in
[Context-dependent rules](#context-dependent-rules).

## Notation

| Notation | Meaning |
|---|---|
| `rule = … ;` | definition |
| `UPPER_CASE` | a token: something the lexer produces and the parser consumes |
| `lower_case` | a helper rule: part of a token or skipped text (lexical grammar), or a nonterminal (syntactic grammar) |
| `"if"` | literal text (keywords are case-insensitive: `IF`, `If` and `if` are the same) |
| `"\n"` `"\r"` `"\t"` `"\f"` `"\v"` | newline, carriage return, tab, form feed, vertical tab |
| `? … ?` | a set of characters described in words |
| `a b` | sequence |
| `a \| b` | alternative |
| `a - b` | `a` except `b` |
| `[ a ]` | optional |
| `{ a }` | zero or more |
| `( a )` | grouping |
| `(* … *)` | comment |

`KEYWORD` and `OPERATOR` are tokens like the others, but the syntactic grammar never refers to them
by name: it writes the specific keyword or operator as a literal, e.g. `"if"` or `"+"`.

## Lexical grammar

The lexer turns the source into tokens. Whitespace, comments and line continuations are skipped
between tokens; newlines are tokens because they end statements and separate matrix rows.

```ebnf
(* ───────── Character classes ───────── *)
character      = ? any character ? ;
letter         = ? any Unicode letter ? ;                     (* so identifiers such as längd are allowed *)
digit          = "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" ;
newline-char   = "\n" | "\r" ;

(* ───────── Skipped between tokens ───────── *)
whitespace     = " " | "\t" | "\f" | "\v" ;
comment        = "%" { character - newline-char } ;           (* up to, not including, the newline *)
continuation   = "..." { character - "\n" } "\n" ;            (* joins the next line to this one *)

(* ───────── Tokens ───────── *)
EOF            = ? end of the input ? ;
NEWLINE        = "\r\n" | "\n" | "\r" ;                       (* ⚑ ignored inside ( ) *)

NUMBER         = digits [ fraction ] [ exponent ]
               | "." digits [ exponent ] ;
fraction       = "." [ digits ] ;                             (* ⚑ not when "." starts an operator *)
exponent       = ( "e" | "E" ) [ "+" | "-" ] digits ;
IMAGINARY      = NUMBER ( "i" | "j" ) ;                       (* ⚑ not followed by a letter, digit or "_" *)
digits         = digit { digit } ;

IDENTIFIER     = ( letter | "_" ) { letter | digit | "_" } ;  (* excluding keywords *)
STRING         = '"' { character - ( '"' | newline-char ) } '"' ;

KEYWORD        = "and" | "begin" | "div" | "do" | "else" | "elseif" | "end" | "for" | "if"
               | "mod" | "not" | "or" | "then" | "to" | "while" | "xor" ;

OPERATOR       = "+" | "-" | "*" | "/" | "\" | "^" | ".*" | "./" | ".\" | ".^"
               | "'" | ".'" | "!" | "="
               | "==" | "~=" | "<>" | "<" | "<=" | ">" | ">="
               | "&" | "&&" | "|" | "||" | "~"
               | "(" | ")" | "[" | "]" | "," | ";" | ":" ;
```

Notes:

- `IDENTIFIER` excludes the keywords, compared case-insensitively: `End` is the keyword `end`.
- The longest operator wins: `<=` is one token, not `<` followed by `=`. `<>` and `~=` both mean
  "not equal".
- Strings use double quotes only; `'` is always the transpose operator.
- A number immediately followed by a letter, digit or `_` (other than an `i`/`j` suffix) is an
  error, e.g. `3x`.

## Syntactic grammar

### Programs and statements

```ebnf
program            = { separator | statement } EOF ;
separator          = NEWLINE | ";" | "," ;

statement          = if-statement
                   | while-statement
                   | for-statement
                   | block
                   | simple-statement ;

simple-statement   = ( assignment | expression ) statement-end ;
assignment         = IDENTIFIER [ argument-list ] "=" expression ;           (* ⚑ lookahead *)
statement-end      = ";"                                                      (* result not shown *)
                   | "," | NEWLINE                                            (* result shown *)
                   | (* not consumed: *) EOF | "else" | "elseif" | "end" ;
```

### Control flow

Each construct comes in two styles. ⚑ The token after the header decides which one is used:
`then`/`do` selects the original style; anything else selects the MATLAB style.

```ebnf
(* ───────── if ───────── *)
if-statement       = "if" expression ( original-if | matlab-if ) ;

original-if        = "then" body
                     [ { NEWLINE | ";" } "else" ( if-statement | body ) ] ;  (* ⚑ else lookahead *)

matlab-if          = header-end statement-list
                     { "elseif" expression header-end statement-list }
                     [ "else" statement-list ]
                     "end" ;

(* ───────── while ───────── *)
while-statement    = "while" expression
                     ( "do" body
                     | header-end statement-list "end" ) ;

(* ───────── for ───────── *)
for-statement      = "for" IDENTIFIER "=" expression [ "to" expression ]
                     ( "do" body
                     | header-end statement-list "end" ) ;

(* ───────── shared ───────── *)
body               = { NEWLINE | ";" } ( block | statement ) ;   (* original style: one statement or a block *)
block              = "begin" { separator | statement } "end" ;
header-end         = NEWLINE | "," | ";" ;                       (* ends a MATLAB-style header *)
statement-list     = { separator | statement } ;                 (* stops before "else", "elseif" or "end" *)
```

`for k = a to b` counts from `a` to `b` in steps of 1. `for k = expression` (without `to`) loops
over the columns of the value, so `for k = 1:2:9` gives 1, 3, 5, 7, 9.

### Expressions

The rules are listed from the lowest to the highest precedence. All binary operators are
left-associative.

```ebnf
expression         = or-expression ;

or-expression      = and-expression { ( "or" | "xor" | "|" | "||" ) and-expression } ;
and-expression     = comparison { ( "and" | "&" | "&&" ) comparison } ;
comparison         = range { ( "==" | "~=" | "<>" | "<" | "<=" | ">" | ">=" ) range } ;
range              = additive [ ":" additive [ ":" additive ] ] ;     (* start:end or start:step:end *)
additive           = multiplicative { ( "+" | "-" ) multiplicative } ;   (* ⚑ matrix elements *)
multiplicative     = unary { ( "*" | "/" | "\" | ".*" | "./" | ".\" | "mod" | "div" ) unary } ;
unary              = unary-operator unary
                   | power ;
power              = postfix { ( "^" | ".^" ) power-operand } ;
power-operand      = unary-operator power-operand                    (* 2^-1 *)
                   | postfix ;
postfix            = primary { "'" | ".'" | "!" } ;

unary-operator     = "+" | "-" | "not" | "~" ;

primary            = NUMBER
                   | IMAGINARY
                   | STRING
                   | IDENTIFIER [ argument-list ]                    (* ⚑ matrix elements *)
                   | ( "mod" | "div" ) argument-list                 (* ⚑ function form *)
                   | "(" expression ")"
                   | matrix ;

argument-list      = "(" [ argument { "," argument } ] ")" ;
argument           = ":"                                             (* ⚑ "all", as in t(1,:) *)
                   | style                                           (* ⚑ as in plot(x, y, +) *)
                   | expression ;
style              = "+" | "-" | "*" | "!" ;

matrix             = "[" { "," | row-separator | element } "]" ;    (* ⚑ element separation *)
row-separator      = ";" | NEWLINE ;
element            = expression ;
```

Because `unary` is below `power`, `-2^2` is `-(2^2)` = −4, as in MATLAB, while `2^-1` = 0.5
through `power-operand`. A unary operator binds tighter than `*`: `-a*b` is `(-a)*b`.

### Precedence summary

| Level | Operators | Associativity |
|---|---|---|
| 1 (lowest) | `or` `xor` `\|` `\|\|` | left |
| 2 | `and` `&` `&&` | left |
| 3 | `==` `~=` `<>` `<` `<=` `>` `>=` | left |
| 4 | `:` (range) | — (at most two colons) |
| 5 | `+` `-` | left |
| 6 | `*` `/` `\` `.*` `./` `.\` `mod` `div` | left |
| 7 | unary `+` `-` `not` `~` | right (prefix) |
| 8 | `^` `.^` | left |
| 9 (highest) | postfix `'` `.'` `!` | left (postfix) |

What the operators *mean* (matrix vs element-wise, MATLAB semantics) is defined by the
interpreter, not the grammar; see the README.

## Context-dependent rules

These rules refine the EBNF above. They are where the parser looks at whitespace, nesting or more
than one token ahead.

1. **Newlines and nesting.** The lexer tracks open brackets. Inside `( … )` newlines are skipped,
   so a call's arguments may span several lines. Inside `[ … ]` a newline is a `NEWLINE` token and
   separates matrix rows. At the top level and in blocks a newline ends a statement.

2. **Numbers next to operators.** In `3.*x` the `.` belongs to the operator `.*`, not to the number:
   a `.` after the digits is part of the number only if it is not followed by `*`, `/`, `\`, `^`
   or `'`. So `3.*x` is `3 .* x`, while `3.5*x` and `3.` are numbers.

3. **Choosing the control-flow style.** After `if expression`, `while expression` and
   `for … = expression [to expression]`, the parser checks the next token:
   - `then` / `do`: original style; the body is one statement (possibly on the next line) or a
     `begin … end` block.
   - `NEWLINE`, `,` or `;`: MATLAB style; the body is a statement list ending with `end`
     (`if` also accepts `elseif` and `else` sections).
   - Anything else is an error: `Expected 'then' or the end of the line, but found …`.

4. **`else` in the original style.** After an original-style `if … then body`, the parser looks
   past newlines and semicolons for `else`. If there is none, it rewinds, so the following
   statement is not affected. An `else` therefore binds to the nearest original-style `if`
   (the classic "dangling else" rule), even inside a MATLAB-style block.

5. **Assignments.** A statement is an assignment when it starts with `IDENTIFIER =`, or with
   `IDENTIFIER ( … ) =` where the parser scans ahead to the matching `)` (on the same line) and
   finds `=`. This separates `x(1,k) = k` (assignment) from `f(x) == 1` (comparison expression).

6. **Matrix elements.** Inside `[ … ]` elements may be separated by `,` or by whitespace:
   - After an element, another element may start without a comma if the next token is preceded by
     whitespace and can start an expression (a number, string, identifier, `(`, `[`, `+`, `-`,
     `not` or `~`).
   - A `+` or `-` that has whitespace before it but not after it starts a new element instead of
     being a binary operator: `[1 -2]` has two elements; `[1 - 2]` and `[1-2]` have one.
   - An identifier followed by `(` with whitespace in between is two elements: `[f (1)]` is
     `f` and `1`, while `[f(1)]` is a call.
   - These rules apply only directly inside brackets: in `[(1 -2)]` the parentheses switch them
     off, giving the single element `-1`.
   - Empty rows (e.g. from a trailing `;` or newline) are ignored.

7. **`mod` and `div`.** These keywords are binary operators (`7 mod 3`, from the original
   language). When directly followed by `(` they are parsed as a function call instead, so
   MATLAB's `mod(k, 2)` also works.

8. **Special arguments.** In an argument list, a lone `:` or a lone `+`, `-`, `*`, `!` directly
   followed by `,` or `)` is an argument by itself: `:` means "all rows/columns" (`t(1,:)`), and
   the others are plot styles (`plot(x, y, +)`). Otherwise they are parsed as operators, so
   `f(-x)` is a negation.

9. **Calls and indexing look the same.** `name(arguments)` is a single syntax form; whether it is a
   function call or indexing into a variable is decided by the interpreter at run time, as in
   MATLAB. Likewise, commands such as `grid` or `cls` are plain identifiers.

## Errors and incomplete input

Syntax errors are recorded with the location of the offending token, and parsing resumes after the
statement (at the next newline or `;`, or before an `end`). A parse result is flagged *incomplete*
when an error occurs at the end of the input — an open `begin` or MATLAB-style block, an unclosed
bracket or parenthesis, or a header without a body — so interactive front-ends can ask for another
line instead of reporting an error.

## Examples

```
% original style
if a > 0 then b = 1 else b = 2
while k <= 10 do
begin
	k = k + 1;
end
for k = 1 to 5 do s = s + k;

% MATLAB style
if x > 0
	s = 1;
elseif x < 0
	s = -1;
else
	s = 0;
end
while n < 3, n = n + 1; end
for k = 1:2:9
	disp(k)
end

% expressions
A = [1 2; 3 4]';
y = -2^2 + 2^-1;          % -4 + 0.5
t(1,:) = [1 -2 3];        % three elements
plot(t(1,:), t(2,:), +)
```
