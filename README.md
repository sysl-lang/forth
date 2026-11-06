# forth

A Forth written in [sysl](https://sysl.sh): a direct-threaded core of Forth 2012's CORE word set,
with cells as wide as the machine's pointer, that passes John Hayes's Core tests.

**It is a sysl stressor first.** A fast Forth asks a language for things most programs never do:
guaranteed tail calls through a table of code addresses, registers that live in parameters across an
unbounded chain of functions, a dictionary that is one flat byte region addressed by plain numbers,
double-width arithmetic, and all of it with no allocator so that it can run on a board. Wherever sysl
could not say one of those directly, the gap goes to sysl to be fixed rather than worked around here.

```
$ forth
forth 0.1.0 -- BYE or Ctrl-D to leave
: SQUARE  DUP * ;
 ok
7 SQUARE .
49  ok
: COUNTDOWN
...   0 SWAP DO I . -1 +LOOP ;
 ok
3 COUNTDOWN
3 2 1 0  ok
```

## Usage

```
sysl build .
./forth                  at a terminal: the REPL
./forth file.fs ...      interpret each file in turn, then end
./forth < file.fs        interpret standard input to its end
```

`BYE` ends the program. A fault — an undefined word, a stack underflow or overflow, a division by
zero, a full dictionary, any `THROW` nothing catches — is reported on a line of its own with the
standard's message for its code, the stacks are emptied, any definition that was being compiled is
taken back out, and interpretation goes on with the next line. Everything defined before the fault
stays. Names are found whatever their case.

## The REPL

`forth` with no file and a terminal on standard input is a traditional Forth prompt, with the line
editor from [`sysl-lang/repl`](https://github.com/sysl-lang/repl):

- **Each line is interpreted when it is entered**, and ` ok` follows it when it ran to its end. A
  fault prints its message instead, and the next line starts fresh with the dictionary as it was.
- **A definition may span lines.** While a `:` definition is open the prompt is `... `, and each line
  is compiled as it is entered. Ctrl-C at that prompt abandons the definition; the data stack stays,
  as `QUIT` leaves it.
- **Tab completes a word from the dictionary**, built-in or your own, whatever the case you type it
  in. One candidate replaces the word; several extend it to their common start and then list.
- **The history is kept in `~/.forth_history`**, one input per line, with Up and Down to walk it. The
  editing keys are `repl`'s: Emacs-style motion and deletion, Ctrl-L to clear.
- `BYE` or Ctrl-D on an empty line leaves.

`KEY` and `ACCEPT` read the same terminal, out of raw mode while a line is being interpreted. Piped
input never starts the REPL: `forth < file.fs` reads the file as it always has, with no prompt.

## The words

297 of them:

- **Stack:** `DUP ?DUP DROP SWAP OVER ROT NIP TUCK 2DUP 2DROP 2SWAP 2OVER >R R> R@ DEPTH PICK ROLL`
- **Arithmetic and logic:** `+ - * / MOD /MOD */ */MOD AND OR XOR INVERT NEGATE ABS 1+ 1- 2* 2/
  LSHIFT RSHIFT MIN MAX = <> < > U< U> 0= 0<> 0< 0> S>D M* UM* UM/MOD SM/REM FM/MOD TRUE FALSE BL`
- **Memory:** `@ ! +! C@ C! 2@ 2! HERE ALLOT , C, ALIGN ALIGNED CELLS CELL+ CHARS CHAR+ COUNT FILL
  MOVE`
- **Control flow:** `IF ELSE THEN BEGIN UNTIL AGAIN WHILE REPEAT DO ?DO LOOP +LOOP I J LEAVE UNLOOP
  EXIT RECURSE EXECUTE`
- **Defining words and the compiler:** `: :NONAME ; CREATE VARIABLE CONSTANT DOES> IMMEDIATE ' ['] [ ]
  LITERAL COMPILE, POSTPONE >BODY STATE`
- **The parser and interpreter:** `WORD PARSE PARSE-NAME FIND EVALUATE SOURCE SOURCE-ID REFILL >IN
  >NUMBER BASE DECIMAL HEX ( \ CHAR [CHAR] S" ." .( ABORT"`
- **Output and input:** `EMIT TYPE CR SPACE SPACES . U. .S <# # #S #> HOLD SIGN KEY ACCEPT WORDS
  ENVIRONMENT?`
- **Leaving:** `ABORT QUIT BYE`
- **Exception:** `CATCH THROW`
- **Core Extension, beyond the above:** `2>R 2R> 2R@ WITHIN VALUE TO DEFER IS DEFER@ DEFER!
  ACTION-OF MARKER BUFFER: [COMPILE] CASE OF ENDOF ENDCASE C" S\" PAD ERASE UNUSED SAVE-INPUT
  RESTORE-INPUT .R U.R HOLDS`
- **String, and `2VARIABLE` from the Double-Number words:** `COMPARE SEARCH -TRAILING /STRING CMOVE
  CMOVE> BLANK SLITERAL REPLACES SUBSTITUTE UNESCAPE 2VARIABLE`
- **Programming-Tools, beyond `.S WORDS BYE STATE` above:** `? DUMP SEE AHEAD CS-PICK CS-ROLL FORGET
  N>R NR> NAME>COMPILE NAME>INTERPRET NAME>STRING SYNONYM TRAVERSE-WORDLIST [DEFINED] [UNDEFINED]
  [IF] [ELSE] [THEN]`. `ASSEMBLER`, `CODE`, `;CODE` and `EDITOR` are left out: what they do is the
  implementation's own (an assembler for the host's instruction set, a text editor), and there is
  neither here.
- **Floating-Point:** `>FLOAT D>F F! F* F+ F- F/ F0< F0= F< F>D F@ FALIGN FALIGNED FCONSTANT FDEPTH
  FDROP FDUP FLITERAL FLOAT+ FLOATS FLOOR FMAX FMIN FNEGATE FOVER FROT FROUND FSWAP FVARIABLE
  REPRESENT`
- **Floating-Point Extension:** `DF! DF@ DFALIGN DFALIGNED DFFIELD: DFLOAT+ DFLOATS F** F. F>S FABS
  FACOS FACOSH FALOG FASIN FASINH FATAN FATAN2 FATANH FCOS FCOSH FE. FEXP FEXPM1 FFIELD: FLN FLNP1 FLOG
  FS. FSIN FSINCOS FSINH FSQRT FTAN FTANH FTRUNC FVALUE F~ PRECISION S>F SET-PRECISION SF! SF@ SFALIGN
  SFALIGNED SFFIELD: SFLOAT+ SFLOATS`

The text interpreter also reads `#123`, `$FF`, `%101` and `'c'`, Forth 2012's number prefixes; a
number with a point after it, `1.` or `-$FF.`, as a double cell; and, while `BASE` is decimal, a float:
`1E`, `1.5E3`, `-2.5e-1`. In any other base `1E` is a number like any other. A `(` comment with no `)`
on its line goes on into the next lines of the console or file, as the File-Access word set has it.

**Division is symmetric**: `/`, `MOD`, `/MOD`, `*/` and `*/MOD` round the quotient toward zero, as C
does, and the remainder takes the dividend's sign. Forth 2012 leaves the choice to the
implementation; `FM/MOD` gives floored division where a program wants it. A double cell is two cells,
low cell deeper, and its arithmetic is done in `DCell`, the integer twice as wide as a cell, chosen by
the target's address width with `#if ptr16` / `ptr32` / `ptr64`:

| target's addresses | cell | `DCell` |
|---|---|---|
| 64 bits (`aarch64`, `x86_64`, `riscv64`) | `isize`, 8 bytes | `i128` |
| 32 bits (`thumb`, `riscv32`, `wasm32`, `x86`) | `isize`, 4 bytes | `i64` |
| 16 bits (`craft`) | `isize`, 2 bytes | `i32` |

**Floats** are IEEE 754 doubles (`real`) on every target, eight bytes whatever a cell is, on a stack of
their own **64 deep**: a word short of floats is `-45`, one pushing past the room `-44`, a fault empties
it as it empties the data stack, and `CATCH` puts its depth back. `FALIGNED` aligns to eight bytes and
`SFALIGNED` to four; `F@` is `DF@`, and `SF@`/`SF!` widen from and round to single precision. The
arithmetic is IEEE's (`1E 0E F/` is an infinity, not a fault) and the functions are `sysl.math`'s.
`REPRESENT`'s digits come from `sysl.fmt.write_real` under `%.*e` — the exact value's digits, rounded
half to even — and `F.`, `FS.` and `FE.` lay out the same digits, `PRECISION` of them (6 to start):
`F.` is fixed point with the zeros after the last significant digit left off (`1E3 F.` is `1000. `,
`1E-10 F.` is `0.0000000001 `), `FS.` is `1.00000E3 ` and `FE.` puts one to three digits before the
point (`20.0000E0 `). An infinity is `inf` or `-inf` and a NaN `nan`; `REPRESENT` answers false for
both, with `INF` or `NAN` in the buffer. `>FLOAT` reads with `sysl.text.parse_real`, correctly
rounded, after rewriting the Forth exponent (`1D2`, `1+2`, `1E`) to the form that reads.

`ENVIRONMENT?` knows `FLOATING`, `FLOATING-EXT`, `FLOATING-STACK` and `MAX-FLOAT`, and answers false
to every other query. There is no `DOUBLE` word set and no `WORDLIST`s yet, so
there is one word list: a name token is a header's address, and `TRAVERSE-WORDLIST` walks the whole
dictionary, newest first, whatever word list it is given.

**What the tools show.** `.S` is `<3> 1 -2 3 `: the depth, then the items deepest first as `.` shows
them. `DUMP` is in hexadecimal whatever `BASE` is, sixteen bytes a line, each line the address, the
bytes, and the bytes as text with a dot for any that is not printable. `SEE` reads a colon definition
back out of its thread, a cell to a line with its offset in cells; a primitive is named by the header
whose code it is, a literal is its number, a string word shows its string, and a branch shows the
offset it goes to:

```
: T 5 0 DO I 2 MOD IF I . ELSE ." x" THEN LOOP ;  SEE T
: T
   0: 5
   2: 0
   4: DO 22
   6: I
   7: 2
   9: MOD
  10: 0BRANCH 17
  12: I
  13: .
  15: BRANCH 20
  17: ." x"
  20: LOOP 6
  22: ;
```

A word made with `DOES>` is listed from the thread after its `DOES>`; a constant, a variable and a
synonym say what made them, and a built-in word says whether it is a primitive or written in sysl.
`FORGET` takes the dictionary back to before the word, and refuses a word the machine was built with
(`-15`, invalid `FORGET`). `[IF]` and `[ELSE]` skip across lines, and a source that ends before the
`[THEN]` is `-58`.

**Every fault is a `THROW`**, with Forth 2012's code for it (-4 for a stack underflow, -13 for an
undefined word, -10 for a division by zero), so `CATCH` catches what the machine raises exactly as it
catches a program's own `THROW`. `ABORT` is `-1 THROW` and `ABORT"` is `-2 THROW`, whose message is
shown only if nothing catches it. Uncaught, a code the standard lists is reported with the standard's
message (`-9 THROW` says `invalid memory address`), an undefined word by its name and `?`, and any
other code as `uncaught exception 42`. `BYE` is the one thing `CATCH` lets through.

## How it is built

Two modules and a program:

| | |
|---|---|
| `sh.sysl.forth` | the machine, the inner and outer interpreters, every word, and `ScriptConsole` — a console over text in memory. **`@no_alloc` and no capability**, so it builds for `thumb-freestanding-softfp` as it stands |
| `sh.sysl.forth.host` | `HostConsole`: standard output, standard input as the keyboard, and the source either a file or standard input; and the REPL's evaluator, console and completion |
| `main.sysl` | `forth` at a terminal, `forth file.fs` and `forth < file.fs` |

**The `host` feature** (on by default, enabling the optional `repl` dependency) is the program, the
host console and the REPL. A board firmware depends on the core alone, in its `sysl.hocon`:

```
dependencies {
  forth { git = "github.com/sysl-lang/forth", version = "0.1.0", default_features = false }
}
```

and supplies a `Console` of its own. `sysl build-c . --target thumb-freestanding-softfp` on a program
that depends on it this way builds the core with no operating system under it. The float words reach
the C library's maths functions (`sin`, `pow`, `expm1`, …) through `sysl.math`, so the firmware links
`-lm`; newlib's, from the Arm GNU Toolchain, has every one they use.

**Cells are two's complement.** Cell arithmetic (`+ - * 1+ 1- NEGATE ABS`, `+!`, `+LOOP`, `CELLS`)
wraps, and a cell read as unsigned or as an address (`U. U< U> RSHIFT`, `C!`, `@`) is its bit
pattern, so none of them traps on a debug build.

**The core talks to the world only through `Console`**: `emit`, `send`, `key`, `accept` and
`refill`. `refill` is where the text interpreter's lines come from and `accept`/`key` are the
keyboard, so a file being run and the keyboard `ACCEPT` reads stay separate. A front end that has its
own lines, as the REPL does, hands each one to `interpret_line` instead and gets back how it ended.

**The dictionary is one region the caller hands over**, fixed in size, holding headers, names,
threads and data. A header is a link cell, a flags byte, a length byte and the name, padded to a
cell; then the code cell (the execution token is its address), a `does` cell, and the body, so
`>BODY` is two cells past the token for every word. The data and return stacks are arrays in the
machine. Addresses are real machine addresses: `@` reads whatever the number names.

## Words written in sysl

A board adds a word written in sysl — a DMA kick, a codec register write, a display blit — with
`define_native` after `boot`. The word is a plain function of the machine, so defining one allocates
nothing:

```sysl
import sh.sysl.forth.{Vm, define_native, need, pop, pop_addr, push, cell_at, throw}

// `REG!` ( x addr -- ): a register write.
reg_store(vm: *Vm)
    if !need(vm, 2) then return

    val addr = pop_addr(vm)

    cell_at(addr)[0] = pop(vm)

// `BLIT` ( x y -- ): draws, or throws 1 when the point is off the screen.
blit(vm: *Vm)
    if !need(vm, 2) then return

    val y = pop(vm)
    val x = pop(vm)

    if x < 0 || x >= 480 || y < 0 || y >= 272 then return throw(vm, 1)

    draw_point(x, y)

// In the firmware's start, after `boot(&machine, …)`:
    define_native(&machine, "REG!", &reg_store)
    define_native(&machine, "BLIT", &blit)
```

From then on it is a word like any built-in: run at the prompt, compiled into a colon definition,
ticked, `EXECUTE`d, found by `FIND`, listed by `WORDS`, described by `SEE`, and redefined by a later
definition of the same name. `immediate = true` makes it `IMMEDIATE`.

- **The stack** is `need(vm, n)` before popping `n` items — it records a stack underflow and answers
  false, and the word returns — then `pop`, `push` and `pop_addr`. `cell_at` and `byte_at` reach
  memory.
- **A fault** is `throw(vm, code)` and a return: the nearest `CATCH` answers the code, or the outer
  interpreter reports it, exactly as for `THROW`.
- **The capacity is the dictionary.** A native word costs a header and one cell of it. `define_native`
  answers 0, or the code `THROW` would use for a refusal — -8 when the dictionary is full, -16 for an
  empty name, -19 for one over 31 bytes — and leaves neither a header nor a fault behind.
- **Dispatch** is the one built-in words written in sysl already use: the word's code is `doslow`,
  which saves the stack registers into the machine, calls the function through the cell after the
  token, and carries on with `NEXT` unless it faulted. The primitives are untouched.

## The threading model, and what `NEXT` compiles to

**Direct threading through guaranteed tail calls.** A thread is a run of cells, each the address of a
primitive, and every primitive has one signature — the machine, the instruction pointer, the data
stack pointer and the top of the stack:

```sysl
type Code = *extern(*Vm, *usize, *isize, isize) -> unit

@inline
next(vm: *Vm, ip: *usize, sp: *isize, tos: isize)
    val f: Code = ptr_cast(ip[0])

    become f(vm, &ip[1], sp, tos)

p_plus(vm: *Vm, ip: *usize, sp: *isize, tos: isize)
    if sp - vm.s0 < 2 then become underflow(vm, ip, sp, tos)

    become next(vm, ip, &sp[-1], sp[0] + tos)
```

`become` is sysl's guaranteed tail call — LLVM's `musttail` — so each primitive *replaces* its frame
with the next one's. A thread of any length runs in one native frame, and the instruction pointer,
stack pointer and top of stack stay in registers the whole way. On AArch64, at `-O1`,
`+` is

```
ldr  x8, [x0, #0x18]     ; the stack's base
sub  x8, x2, x8
cmp  x8, #0xf            ; two items?
b.gt 1f
...                      ; the underflow path: save the registers, record -4, ret
1: ldr  x8, [x2], #-0x8  ; second item, and pop
   ldr  x4, [x1], #0x8   ; NEXT: load the next cell, step ip
   add  x3, x8, x3
   br   x4               ; NEXT: jump
```

so **`NEXT` is two instructions, `ldr x4, [x1], #8` and `br x4`**, with no call, no return and no
dispatch loop. A fault ends the chain by *returning* instead: every frame in between was replaced, so
the return lands in `run`, which started the thread.

**That return is `THROW`, and `CATCH` is a `run`.** `THROW` records its code in the machine and
returns; `CATCH` notes the data stack's depth, the return stack pointer and the input source, and runs
its token in a nested `run`. However deep the `THROW` — colon definitions, `EXECUTE`, `EVALUATE`
inside `EVALUATE` — it returns through every sysl frame in between to that `run`, and `CATCH` puts the
three back and answers the code. No `setjmp`, no unwinding tables, and the hot path is unchanged.

A word that is not a primitive is compiled as two cells: the address of its code (`docol` for a colon
definition, `dovar`, `doconst`, `dodoes`, or `doslow` for a word written in sysl) and its execution
token, which that code reads as an operand. `docol` pushes the return address and jumps to the body —
on AArch64, `ldr x8, [x1]; ldr x4, [x8, #0x10]; add x1, x8, #0x18; br x4` after the return-stack
push. The parser, the compiler and output are ordinary sysl functions reached through `doslow`; none
is on a hot path.

On `thumb-freestanding-softfp` the same `+` is a tail call too, but LLVM keeps the frame pointer and
saves `r7` and `lr` around the jump, so a Cortex-M `NEXT` costs a push and a pop besides.

## Tests

`sysl test .` runs 134 tests (120 with `--no-default-features`, which leaves out the host module's):

- **The Hayes Core tests: 638 of 638 pass.** `tests/tester.fr` and `tests/core.fr` are John Hayes's
  files from [gerryjackson/forth2012-test-suite](https://github.com/gerryjackson/forth2012-test-suite)
  at commit `4c87e5659c5b79bd7a6664daa56603df080c6f9b`, unchanged and with their copyright notices;
  `tests/count.fs` counts the tests as they run. The sysl test embeds all three and runs them through
  a `ScriptConsole`. From a shell, the same run is
  `./forth tests/tester.fr tests/count.fs tests/core.fr tests/report.fs < /dev/null`, which prints
  `tests: 638 failed: 0` after core.fr's own output.
- **The Forth 2012 suite's word-set files**, from the same commit, unchanged, each run by a sysl
  test after `tester.fr` and `core.fr` (and the suite's helpers where the file needs them), with
  `count.fs` loaded just before it. Each test asserts the number of tests, zero errors in tester's
  count, and that no line faulted:

  | file | upstream path | SHA-256 | tests | errors |
  |---|---|---|---:|---:|
  | `tests/coreplustest.fth` | `src/coreplustest.fth` | `d967d1d367b3c0bf91b13944694fb5ac26fa793a63be3ea58bf50779971ef47b` | 101 | 0 |
  | `tests/coreexttest.fth` | `src/coreexttest.fth` | `f35b9a78ac463985e2cfa616f19893204e25f5a51f0512324d8d45fe9e73b71b` | 383 | 0 |
  | `tests/exceptiontest.fth` | `src/exceptiontest.fth` | `92bce976b74a799f0a7c9f4200e46832eea6371d117e522dea4550a6fe067083` | 9 | 0 |
  | `tests/stringtest.fth` | `src/stringtest.fth` | `6f78aafab33acf2c3e09ec428447d539e2f8afe71fa3ca2ed329b2999b129cde` | 135 | 0 |
  | `tests/toolstest.fth` | `src/toolstest.fth` | `d410235d133d9eb6f0b322a3b1ac647354cbe12f37d1ce8a3f2cb2317a5faa7f` | 50 | 0 |

  `toolstest.fth` skips its `TRAVERSE-WORDLIST` and `NAME>` tests where the Search-Order words are
  missing, as they are here; the unit tests below cover those words instead.

  The suite's helpers, loaded before the word-set files as its own `runtests.fth` loads them:
  `tests/utilities.fth` (`src/utilities.fth`,
  `69b362b974f2ecebb0dbea1d77281dcca7b112107d7ad7b3e7f764a40c1c83a7`) and `tests/errorreport.fth`
  (`src/errorreport.fth`, `d348c8d5d2ad1793b118f83dda6f79d4c74c3d7abd973e8ad7d8841b8243366d`). A
  word-set file hands its error count to `errorreport.fth`'s `SET-ERROR-COUNT` at its end, so the
  test reads it from there, and checks the total over every file too.
- **The suite's floating-point files**, from `src/fp/` at the same commit, unchanged, each run by a
  sysl test after the suite's own floating-point tester, `tests/fp/ttester.fs`
  (`src/fp/ttester.fs`, `76948bb8f1bb343a488aa5fb744b115f9026caf8eebb142906bae6b29e6b067b`), and
  `tests/fp/count.fs`, which counts the tests and ttester's errors and defines the two words the
  files use that are not this system's: `F>`, which is no standard word, and `D>S`, from the
  Double-Number word set. Each test asserts the number of tests, zero errors, that no line faulted
  and that the floating-point stack ends empty:

  | file | SHA-256 | tests | errors |
  |---|---|---:|---:|
  | `tests/fp/fatan2-test.fs` | `861de3aba5166be5333c241d3f9f810ad017d71f2e0b6d0a352cdb98bc00a1ea` | 39 | 0 |
  | `tests/fp/ieee-arith-test.fs` | `f6d07b22628dabbc2829283888ab6a71f1e8d26d72c14050862ac4625d30190d` | 172 | 0 |
  | `tests/fp/ieee-fprox-test.fs` | `9d0683894cc1545f0259e717f97d21471dfcdaddc7a508bb8d5ca2b302697be8` | 242 | 0 |
  | `tests/fp/fpzero-test.4th` | `59906ee261053662c21d2bec7780c277c9159731e85d88b8b94282c609e7d292` | 12 | 0 |
  | `tests/fp/fpio-test.4th` | `84ffb3cdc67f40a5a24a4bf4eed2f09ca48bbd9a61f3823421b960d76ec2d82a` | 110 | 0 |
  | `tests/fp/to-float-test.4th` | `cd9af313a9bdef25ba67d2dfbffa26191721d74a193faf79c48ed5dafbaed639` | 35 | 0 |
  | `tests/fp/ak-fp-test.fth` | `653873d89fe3fadc1d72da4c8874566bfb23c86d5620ee92e6852b9cf0733515` | 214 | 0 |
  | `tests/fp/paranoia.4th` | `8d5185dbd4a957b86946585cdf9b043143ebe58ef276add73ba975800e365f9c` | — | 0 |

  `paranoia.4th` is Kahan's diagnosis rather than a list of tests; the test asserts that its failure,
  serious-defect, defect and flaw counts are all zero and that it calls the arithmetic `Excellent!`.
  `ak-fp-test.fth`'s `F.`, `FE.` and `FS.` lines are shown for a reader to compare, not counted.
- The inner interpreter: a hand-built thread, a fault leaving through `run`, nested colon
  definitions, ten million loop iterations in one native frame, `EXECUTE` of every kind of word,
  `DOES>`, `LEAVE`/`UNLOOP`, `+LOOP` across zero.
- The dictionary: the header layout byte for byte, what `,` `C,` `ALLOT` and `ALIGN` lay down, search
  order, `IMMEDIATE`, case.
- The number parser: prefixes, signs, characters, refusals, `BASE`, `>NUMBER`, the cell's full range.
- Every fault: underflow at the prompt and inside a thread, an undefined word at the prompt and inside
  a definition, a dictionary full by `ALLOT` and while compiling, division by zero in each of the
  seven dividing words, return-stack overflow, data-stack overflow, `QUIT`, `ABORT`, `ABORT"`, `BYE`.
- `CATCH` and `THROW`: a `THROW` from nested definitions with both stacks put back, from inside
  `EXECUTE` inside a `DO` loop that goes on after, the machine's own faults caught with their codes
  and the input source put back, `0 THROW`, the standard messages and `uncaught exception n` when
  nothing catches, `ABORT"`'s message shown only uncaught, `BYE` going through `CATCH`, and a thrown
  `1` not ending the program.
- The Core Extension words where the suite does not reach: an empty `DEFER`, `MARKER`, `CASE` with no
  match, `S\"` and `C"` interpreted, `RESTORE-INPUT` of another source, `2R>` underflow, `.R` fields,
  a negative `BUFFER:`.
- The Programming-Tools words where the suite does not reach: `.S` with no, one and many items,
  negative ones and another `BASE`; `?`; `DUMP`'s lines byte for byte; `SEE` of a definition with a
  loop, an `IF`/`ELSE`, literals and strings, one with an `EXIT` a branch jumps over, a `DOES>` word, a
  constant, a variable, a synonym and the built-in kinds; `WORDS`; `FORGET` putting `HERE` back and
  an older word of the name, and refusing a built-in; nested `[IF]` across lines and a missing
  `[THEN]`; `N>R` faults; `SYNONYM` of an immediate word; `TRAVERSE-WORDLIST` stopping early; and
  `NAME>COMPILE`.
- The Floating-Point words where the suite does not reach: float literals and what is not one, `1E`
  in hexadecimal, double-cell literals, `>FLOAT`'s `D` and sign-only exponents; `F.` of zero, `-0`,
  the infinities, a NaN, huge and tiny and subnormal values; `PRECISION` at 1, 3, 6 and 17, ties to
  even; `REPRESENT` rounding into the exponent, of `-0` and of an infinity; `SEE` of a float literal;
  underflow (`-45`) and overflow (`-44`) of the floating-point stack and `CATCH` putting its depth
  back; the two stacks kept apart and a fault emptying both; `F>S`, `F>D`, `FTRUNC`, `FROUND`,
  `FATANH`; float alignment and sizes, an `FVARIABLE` after an odd `HERE`; `FCONSTANT`, `FVALUE` and
  `TO` now and compiled; the field words' offsets; `SF!` rounding; the `ENVIRONMENT?` queries; and a
  `(` comment across lines.
- The host console's line splitting, and `interpret_line`'s answers.
- The REPL, driven through `repl`'s `MockTerminal` with scripted keys and checked against the exact
  bytes sent to the terminal: ` ok` after a line, a definition across two lines at the continuation
  prompt, an error followed by a working line, an uncaught `THROW` showing the standard message and
  the REPL going on, a caught one answered with ` ok`, an error inside a definition, Ctrl-C
  abandoning a definition, Tab completing a built-in word in lower case and a word of the user's,
  which names completion offers, `BYE`, and Ctrl-D.

## Speed

Against two mature Forths written in C: [gforth](https://gforth.org) 0.7.3 from Homebrew, both its
default engine (`gforth`) and its fastest (`gforth-fast`), and
[pForth](https://github.com/philburk/pforth) built from source at `-O2` (commit
`e5617638c3a397c644fe82c6301711867a0ca1bf`). This forth is built at `-O2`, which the manifest sets.
Seconds of wall time, the median of five runs, the systems taking turns within each round:

| benchmark | forth | gforth | gforth-fast | pForth |
|---|---:|---:|---:|---:|
| sieve | 1.052 | 1.246 | 0.787 | 2.742 |
| fib | 0.634 | 0.645 | 0.395 | 1.256 |
| nest | 0.519 | 0.799 | 0.551 | 1.162 |
| bubble | 0.817 | 0.616 | 0.345 | 1.168 |

- **sieve** — the BYTE sieve of the primes below 16384, 8000 times over an 8190-byte flag array:
  `C@`, `C!`, `DO` loops and a `BEGIN … WHILE … REPEAT`.
- **fib** — `38 FIB`, doubly recursive: calls and returns, and little else.
- **nest** — two nested `DO` loops around `I J * + 7 XOR`, two hundred million passes.
- **bubble** — a bubble sort of 1000 cells filled by a linear congruential generator, a hundred
  times: `@`, `!`, comparisons and a branch per pair.

On an Apple M5 Pro (18 cores, 64 GB), macOS 27.0.1. The sources are in `bench/` and run unchanged on
all three systems; `zsh bench/run.zsh` builds forth and prints the table, checking that every system
printed the same answer. Each time includes starting the system, which is a few milliseconds.

**forth is between gforth and gforth-fast**: ahead of the default gforth on three of the four, level
with gforth-fast on the loop kernel, and about twice as fast as pForth throughout. Every forth
primitive checks the stack depth before it touches the stack, which gforth-fast does not; where
gforth-fast's lead on fib and bubble comes from has not been measured yet.

## What's next

- **A Pico 2 front end**: the core already builds for `thumb-freestanding-softfp` with a
  `ScriptConsole`; what is missing is a `Console` over the USB serial line and a region in SRAM.

## License

ISC — see `LICENSE`. The files under `tests/` from the Forth 2012 test suite keep their own notice:
*(C) 1995 Johns Hopkins University / Applied Physics Laboratory; may be distributed freely as long as
this copyright notice remains.*
