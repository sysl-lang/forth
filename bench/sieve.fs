\ The BYTE sieve: the primes below 16384, found 8000 times over an 8190-byte flag array.
\ Prints 1899, the count the original benchmark checks.

8190 CONSTANT SIZE
CREATE FLAGS SIZE ALLOT

: SIEVE ( -- n )
  FLAGS SIZE 1 FILL
  0 SIZE 0 DO
    FLAGS I + C@ IF
      I DUP + 3 + DUP I +
      BEGIN DUP SIZE < WHILE 0 OVER FLAGS + C! OVER + REPEAT
      DROP DROP 1+
    THEN
  LOOP ;

: BENCH  0 8000 0 DO DROP SIEVE LOOP . CR ;

BENCH
BYE
