\ Doubly recursive Fibonacci: calls and returns, and little else. Prints 63245986.

: FIB ( n -- f )  DUP 2 < IF EXIT THEN  1- DUP RECURSE  SWAP 1- RECURSE + ;

38 FIB . CR
BYE
