\ Loaded after ttester.fs and before a test file: counts the tests as they run and the errors
\ ttester reports, so that a run can be checked for every test and no error. ttester's own T{ goes on
\ doing its work, and its ERROR goes on reporting.

DECIMAL
VARIABLE #TESTS   0 #TESTS !
VARIABLE #ERRORS  0 #ERRORS !
: T{   1 #TESTS +! T{ ;
: COUNTED-ERROR   1 #ERRORS +! ERROR1 ;
' COUNTED-ERROR ERROR-XT !

\ Two words the files use that are not this system's: fpzero-test.4th's F>, which is no standard
\ word, and paranoia.4th's D>S, which is the Double-Number word set's.
[UNDEFINED] F>  [IF] : F>   FSWAP F< ; [THEN]
[UNDEFINED] D>S [IF] : D>S  DROP ; [THEN]
