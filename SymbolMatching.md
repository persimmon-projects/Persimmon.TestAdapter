# TODO: Symbol matching...

## Failed fixup
* failure (context)
* success (context)

## Label not followed
* test1
* test2 (global)
* test2 (MyClass)
* test3
* test4
* tests5
  * case parameterize test
* tests6
  * source parameterize test

## No pickup
* invalid: tests
  * success test(list) : list
  * failure test(list) : list
* invalid: tests2
  * success test1(array) : array
  * success test2(array) : array
  * failure test(array) : array
* invalid: tests3
  * failure test(seq) : seq
* invalid tests4
  * success test(list with value)
  * failure test(list with value)

## Parameter expected demangle
* should output full name
* printer should print list in a row
